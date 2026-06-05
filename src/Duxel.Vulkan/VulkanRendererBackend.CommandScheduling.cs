using System.Buffers;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const int MaxCommandScheduleCacheEntries = 64;
    private readonly VulkanCommandSchedulerMode _commandSchedulerMode = ParseCommandSchedulerMode();
    private readonly int _commandSchedulerMaxWindow = ParsePositiveIntEnvironment("DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW", 768);
    private readonly Dictionary<ulong, CommandScheduleCacheEntry> _commandScheduleCache = new();

    private enum VulkanCommandSchedulerMode
    {
        Disabled,
        Static,
        All,
    }

    private enum CommandSchedulingClass
    {
        Solid = 0,
        Font = 1,
        ColorTriangle = 2,
        TexturedTriangle = 3,
        ColorPrimitive = 4,
        TexturedPrimitive = 5,
    }

    private enum CommandScheduleResult
    {
        Disabled,
        Skipped,
        NoChange,
        CacheHit,
        CacheMiss,
    }

    private readonly struct ScheduledCommandMergeResult(
        UiDrawCommand command,
        int commandOrderIndex,
        int mergedCommandCount)
    {
        public UiDrawCommand Command { get; } = command;
        public int CommandOrderIndex { get; } = commandOrderIndex;
        public int MergedCommandCount { get; } = mergedCommandCount;
    }

    private readonly struct CommandIterationState(int commandCount, int[]? scheduledOrder)
    {
        public int CommandCount { get; } = commandCount;
        public int[]? ScheduledOrder { get; } = scheduledOrder;
        public bool HasScheduledOrder => ScheduledOrder is not null;
    }

    private readonly struct CommandIterationStep(
        UiDrawCommand command,
        int commandIndex,
        int nextCommandOrderIndex,
        int mergedCommandCount)
    {
        public UiDrawCommand Command { get; } = command;
        public int CommandIndex { get; } = commandIndex;
        public int NextCommandOrderIndex { get; } = nextCommandOrderIndex;
        public int MergedCommandCount { get; } = mergedCommandCount;
    }

    private sealed class CommandScheduleCacheEntry(int commandCount, int[]? order, CommandScheduleCacheCommandKey[] keys)
    {
        public int CommandCount { get; } = commandCount;
        public int[]? Order { get; } = order;
        public CommandScheduleCacheCommandKey[] Keys { get; } = keys;
    }

    private readonly record struct CommandScheduleCacheCommandKey(
        UiRect ClipRect,
        UiTextureId TextureId,
        uint ElementCount,
        UiVector2 Translation,
        UiDrawCommandKind Kind,
        UiRect Bounds,
        bool HasBounds,
        bool HasCallback);

    private CommandScheduleResult TryGetScheduledCommandOrder(
        UiDrawList drawList,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        out int[]? order)
    {
        order = null;
        var isStaticLayerReplay = !string.IsNullOrWhiteSpace(drawList.StaticGeometryKey)
            && IsStaticLayerGeometryTag(drawList.StaticGeometryKey, out _);
        if (!CanScheduleDrawList(isStaticLayerReplay))
        {
            return CommandScheduleResult.Disabled;
        }

        var commandCount = drawList.Commands.Count;
        if (commandCount < 4)
        {
            return CommandScheduleResult.Skipped;
        }

        var hasDrawListScheduleStamp = drawList.CommandScheduleStamp is not 0;
        var hash = hasDrawListScheduleStamp
            ? drawList.CommandScheduleStamp
            : ComputeCommandScheduleHash(drawList, hasStaticBinding, in staticBinding);
        if (_commandScheduleCache.TryGetValue(hash, out var cached)
            && cached.CommandCount == commandCount
            && (hasDrawListScheduleStamp || CommandScheduleCacheEntryMatches(cached, drawList)))
        {
            order = cached.Order;
            return order is null
                ? CommandScheduleResult.NoChange
                : CommandScheduleResult.CacheHit;
        }

        var scheduledOrder = new int[commandCount];
        for (var i = 0; i < commandCount; i++)
        {
            scheduledOrder[i] = i;
        }

        var changed = false;
        var start = 0;
        while (start < commandCount)
        {
            while (start < commandCount && !CanScheduleCommand(in drawList.Commands.ItemRef(start)))
            {
                start++;
            }

            if (start >= commandCount)
            {
                break;
            }

            var end = start + 1;
            while (end < commandCount && CanScheduleCommand(in drawList.Commands.ItemRef(end)))
            {
                end++;
            }

            while (start < end)
            {
                var length = Math.Min(_commandSchedulerMaxWindow, end - start);
                if (length >= 4)
                {
                    changed |= ScheduleCommandWindow(
                        drawList,
                        start,
                        length,
                        hasStaticBinding,
                        isStaticLayerReplay,
                        in staticBinding,
                        scheduledOrder);
                }

                start += length;
            }
        }

        if (!changed)
        {
            CacheCommandSchedule(
                hash,
                commandCount,
                order: null,
                hasDrawListScheduleStamp ? [] : CreateCommandScheduleCacheKeys(drawList));
            return CommandScheduleResult.NoChange;
        }

        CacheCommandSchedule(
            hash,
            commandCount,
            scheduledOrder,
            hasDrawListScheduleStamp ? [] : CreateCommandScheduleCacheKeys(drawList));
        order = scheduledOrder;
        return CommandScheduleResult.CacheMiss;
    }

    private void CacheCommandSchedule(
        ulong hash,
        int commandCount,
        int[]? order,
        CommandScheduleCacheCommandKey[] keys)
    {
        if (_commandScheduleCache.Count >= MaxCommandScheduleCacheEntries)
        {
            _commandScheduleCache.Clear();
        }

        _commandScheduleCache[hash] = new CommandScheduleCacheEntry(commandCount, order, keys);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanScheduleDrawList(bool isStaticLayerReplay)
    {
        return _commandSchedulerMode switch
        {
            VulkanCommandSchedulerMode.All => true,
            VulkanCommandSchedulerMode.Static => isStaticLayerReplay,
            _ => false,
        };
    }

    private static VulkanCommandSchedulerMode ParseCommandSchedulerMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_COMMAND_SCHEDULER");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return VulkanCommandSchedulerMode.Disabled;
        }

        var value = raw.Trim();
        if (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanCommandSchedulerMode.All;
        }

        if (string.Equals(value, "static", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "static-layer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "static-layers", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "stable", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanCommandSchedulerMode.Static;
        }

        return VulkanCommandSchedulerMode.Disabled;
    }

    private static CommandIterationState CreateCommandIterationState(
        UiDrawList drawList,
        int[]? scheduledOrder)
    {
        return new CommandIterationState(drawList.Commands.Count, scheduledOrder);
    }

    private static bool TryGetNextCommandIterationStep(
        UiDrawList drawList,
        in CommandIterationState state,
        int commandOrderIndex,
        out CommandIterationStep step)
    {
        var currentOrderIndex = commandOrderIndex;
        while (currentOrderIndex < state.CommandCount)
        {
            var commandIndex = state.HasScheduledOrder
                ? state.ScheduledOrder![currentOrderIndex]
                : currentOrderIndex;
            var command = drawList.Commands[commandIndex];
            if (command.ElementCount == 0 && command.Callback is null)
            {
                currentOrderIndex++;
                continue;
            }

            var mergedCommandCount = 0;
            if (state.HasScheduledOrder)
            {
                var mergeResult = MergeScheduledCommandRun(
                    drawList,
                    state.ScheduledOrder!,
                    currentOrderIndex,
                    state.CommandCount,
                    in command);
                command = mergeResult.Command;
                currentOrderIndex = mergeResult.CommandOrderIndex;
                mergedCommandCount = mergeResult.MergedCommandCount;
            }

            step = new CommandIterationStep(
                command,
                commandIndex,
                currentOrderIndex,
                mergedCommandCount);
            return true;
        }

        step = default;
        return false;
    }

    private static ulong ComputeCommandScheduleHash(
        UiDrawList drawList,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding)
    {
        var hash = 14695981039346656037UL;
        hash = HashScheduleValue(hash, (uint)drawList.Commands.Count);
        hash = HashScheduleValue(hash, hasStaticBinding ? 1u : 0u);
        hash = HashScheduleValue(hash, hasStaticBinding && staticBinding.HasExpandedPrimitiveGeometry ? 1u : 0u);
        for (var i = 0; i < drawList.Commands.Count; i++)
        {
            var key = CreateCommandScheduleCacheKey(in drawList.Commands.ItemRef(i));
            hash = HashScheduleValue(hash, key.ElementCount);
            hash = HashScheduleValue(hash, key.TextureId.Value);
            hash = HashScheduleValue(hash, (uint)key.Kind);
            hash = HashScheduleValue(hash, key.HasBounds ? 1u : 0u);
            hash = HashScheduleValue(hash, key.HasCallback ? 1u : 0u);
            hash = HashScheduleValue(hash, key.ClipRect.X);
            hash = HashScheduleValue(hash, key.ClipRect.Y);
            hash = HashScheduleValue(hash, key.ClipRect.Width);
            hash = HashScheduleValue(hash, key.ClipRect.Height);
            hash = HashScheduleValue(hash, key.Translation.X);
            hash = HashScheduleValue(hash, key.Translation.Y);
            hash = HashScheduleValue(hash, key.Bounds.X);
            hash = HashScheduleValue(hash, key.Bounds.Y);
            hash = HashScheduleValue(hash, key.Bounds.Width);
            hash = HashScheduleValue(hash, key.Bounds.Height);
        }

        return hash;
    }

    private static bool CommandScheduleCacheEntryMatches(CommandScheduleCacheEntry entry, UiDrawList drawList)
    {
        if (entry.CommandCount != drawList.Commands.Count || entry.Keys.Length != drawList.Commands.Count)
        {
            return false;
        }

        for (var i = 0; i < drawList.Commands.Count; i++)
        {
            if (entry.Keys[i] != CreateCommandScheduleCacheKey(in drawList.Commands.ItemRef(i)))
            {
                return false;
            }
        }

        return true;
    }

    private static CommandScheduleCacheCommandKey[] CreateCommandScheduleCacheKeys(UiDrawList drawList)
    {
        var keys = new CommandScheduleCacheCommandKey[drawList.Commands.Count];
        for (var i = 0; i < drawList.Commands.Count; i++)
        {
            keys[i] = CreateCommandScheduleCacheKey(in drawList.Commands.ItemRef(i));
        }

        return keys;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CommandScheduleCacheCommandKey CreateCommandScheduleCacheKey(in UiDrawCommand cmd)
    {
        return new CommandScheduleCacheCommandKey(
            cmd.ClipRect,
            cmd.TextureId,
            cmd.ElementCount,
            cmd.Translation,
            cmd.Kind,
            cmd.Bounds,
            cmd.HasBounds,
            cmd.Callback is not null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashScheduleValue(ulong hash, float value)
    {
        return HashScheduleValue(hash, BitConverter.SingleToUInt32Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashScheduleValue(ulong hash, nuint value)
    {
        return HashScheduleValue(hash, unchecked((ulong)value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashScheduleValue(ulong hash, ulong value)
    {
        hash = HashScheduleValue(hash, unchecked((uint)value));
        return HashScheduleValue(hash, unchecked((uint)(value >> 32)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashScheduleValue(ulong hash, uint value)
    {
        hash ^= value & 0xFFu;
        hash *= 1099511628211UL;
        hash ^= (value >> 8) & 0xFFu;
        hash *= 1099511628211UL;
        hash ^= (value >> 16) & 0xFFu;
        hash *= 1099511628211UL;
        hash ^= (value >> 24) & 0xFFu;
        hash *= 1099511628211UL;
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanScheduleCommand(in UiDrawCommand cmd)
    {
        return cmd.ElementCount > 0
            && cmd.Callback is null
            && cmd.HasBounds
            && cmd.Bounds.Width > 0f
            && cmd.Bounds.Height > 0f;
    }

    private bool ScheduleCommandWindow(
        UiDrawList drawList,
        int start,
        int length,
        bool hasStaticBinding,
        bool useConservativeBounds,
        in StaticGeometryBuffer staticBinding,
        int[] order)
    {
        var indegrees = ArrayPool<int>.Shared.Rent(length);
        var selected = ArrayPool<int>.Shared.Rent(length);
        var classes = ArrayPool<CommandSchedulingClass>.Shared.Rent(length);
        var bounds = ArrayPool<UiRect>.Shared.Rent(length);

        try
        {
            for (var i = 0; i < length; i++)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(start + i);
                indegrees[i] = 0;
                selected[i] = 0;
                classes[i] = GetSchedulingClass(in cmd, hasStaticBinding, in staticBinding);
                bounds[i] = GetEffectiveSchedulingBounds(in cmd, useConservativeBounds);
            }

            for (var i = 0; i < length - 1; i++)
            {
                var a = bounds[i];
                for (var j = i + 1; j < length; j++)
                {
                    if (Overlaps(a, bounds[j]))
                    {
                        indegrees[j]++;
                    }
                }
            }

            var changed = false;
            var hasLastClass = false;
            var lastClass = default(CommandSchedulingClass);
            for (var output = 0; output < length; output++)
            {
                var pick = PickNextScheduledCommand(indegrees, selected, classes, length, hasLastClass, lastClass);
                if (pick < 0)
                {
                    return false;
                }

                selected[pick] = 1;
                var sourceIndex = start + pick;
                order[start + output] = sourceIndex;
                if (sourceIndex != start + output)
                {
                    changed = true;
                }

                hasLastClass = true;
                lastClass = classes[pick];

                var pickedBounds = bounds[pick];
                for (var j = pick + 1; j < length; j++)
                {
                    if (selected[j] == 0 && Overlaps(pickedBounds, bounds[j]))
                    {
                        indegrees[j]--;
                    }
                }
            }

            return changed;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indegrees, clearArray: false);
            ArrayPool<int>.Shared.Return(selected, clearArray: false);
            ArrayPool<CommandSchedulingClass>.Shared.Return(classes, clearArray: false);
            ArrayPool<UiRect>.Shared.Return(bounds, clearArray: false);
        }
    }

    private static int PickNextScheduledCommand(
        int[] indegrees,
        int[] selected,
        CommandSchedulingClass[] classes,
        int length,
        bool hasLastClass,
        CommandSchedulingClass lastClass)
    {
        var pick = -1;
        if (hasLastClass)
        {
            for (var i = 0; i < length; i++)
            {
                if (selected[i] == 0 && indegrees[i] == 0 && classes[i] == lastClass)
                {
                    return i;
                }
            }
        }

        var bestPriority = int.MaxValue;
        for (var i = 0; i < length; i++)
        {
            if (selected[i] != 0 || indegrees[i] != 0)
            {
                continue;
            }

            var priority = GetSchedulingPriority(classes[i]);
            if (priority < bestPriority)
            {
                bestPriority = priority;
                pick = i;
            }
        }

        return pick;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CommandSchedulingClass GetSchedulingClass(
        in UiDrawCommand cmd,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding)
    {
        var isTriangleCommand = cmd.Kind is UiDrawCommandKind.Triangles;
        if (isTriangleCommand)
        {
            if (_triangleColorPipelineEnabled && IsWhiteTextureId(cmd.TextureId))
            {
                return CommandSchedulingClass.ColorTriangle;
            }

            return IsFontTextureId(cmd.TextureId)
                ? CommandSchedulingClass.Font
                : CommandSchedulingClass.TexturedTriangle;
        }

        var primitiveUsesTexture = !IsWhiteTextureId(cmd.TextureId);
        if (primitiveUsesTexture)
        {
            return CommandSchedulingClass.TexturedPrimitive;
        }

        var staticPrimitiveUsesTriangleGeometry = hasStaticBinding
            && staticBinding.HasExpandedPrimitiveGeometry;
        var canUseSolidPipeline = _solidUnifiedPipelineEnabled
            && (!hasStaticBinding || _solidUnifiedStaticEnabled || staticPrimitiveUsesTriangleGeometry);
        return canUseSolidPipeline
            ? CommandSchedulingClass.Solid
            : CommandSchedulingClass.ColorPrimitive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSchedulingPriority(CommandSchedulingClass schedulingClass)
    {
        return schedulingClass switch
        {
            CommandSchedulingClass.Solid => 0,
            CommandSchedulingClass.ColorPrimitive => 1,
            CommandSchedulingClass.ColorTriangle => 2,
            CommandSchedulingClass.Font => 3,
            CommandSchedulingClass.TexturedPrimitive => 4,
            _ => 5,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UiRect GetEffectiveSchedulingBounds(in UiDrawCommand cmd, bool useConservativeBounds)
    {
        var bounds = cmd.Bounds;
        if (useConservativeBounds)
        {
            return bounds;
        }

        if (cmd.Translation == default)
        {
            return IntersectBounds(bounds, cmd.ClipRect);
        }

        var translatedBounds = new UiRect(
            bounds.X + cmd.Translation.X,
            bounds.Y + cmd.Translation.Y,
            bounds.Width,
            bounds.Height);
        var translatedClip = new UiRect(
            cmd.ClipRect.X + cmd.Translation.X,
            cmd.ClipRect.Y + cmd.Translation.Y,
            cmd.ClipRect.Width,
            cmd.ClipRect.Height);
        return IntersectBounds(translatedBounds, translatedClip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UiRect IntersectBounds(UiRect a, UiRect b)
    {
        var x1 = MathF.Max(a.X, b.X);
        var y1 = MathF.Max(a.Y, b.Y);
        var x2 = MathF.Min(a.X + a.Width, b.X + b.Width);
        var y2 = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        return x2 <= x1 || y2 <= y1
            ? default
            : new UiRect(x1, y1, x2 - x1, y2 - y1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Overlaps(UiRect a, UiRect b)
    {
        return a.Width > 0f
            && a.Height > 0f
            && b.Width > 0f
            && b.Height > 0f
            && a.X < b.X + b.Width
            && b.X < a.X + a.Width
            && a.Y < b.Y + b.Height
            && b.Y < a.Y + a.Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanMergeScheduledCommands(in UiDrawCommand a, in UiDrawCommand b)
    {
        return a.Callback is null
            && b.Callback is null
            && a.TextureId == b.TextureId
            && a.ClipRect == b.ClipRect
            && a.Translation == b.Translation
            && a.Kind == b.Kind
            && a.VertexOffset == b.VertexOffset
            && a.IndexOffset + a.ElementCount == b.IndexOffset
            && MathF.Abs(a.Opacity - b.Opacity) <= 0.000001f
            && Equals(a.UserData, b.UserData);
    }

    private static ScheduledCommandMergeResult MergeScheduledCommandRun(
        UiDrawList drawList,
        int[] scheduledOrder,
        int commandOrderIndex,
        int commandCount,
        in UiDrawCommand command)
    {
        var mergedCommand = command;
        var mergedCommandCount = 0;
        var currentOrderIndex = commandOrderIndex;

        while (currentOrderIndex + 1 < commandCount)
        {
            var nextCommandIndex = scheduledOrder[currentOrderIndex + 1];
            var nextCommand = drawList.Commands[nextCommandIndex];
            if (nextCommand.ElementCount == 0 && nextCommand.Callback is null)
            {
                break;
            }

            if (!CanMergeScheduledCommands(in mergedCommand, in nextCommand))
            {
                break;
            }

            mergedCommand = mergedCommand with
            {
                ElementCount = mergedCommand.ElementCount + nextCommand.ElementCount,
                Bounds = mergedCommand.HasBounds && nextCommand.HasBounds
                    ? MergeSchedulingBounds(mergedCommand.Bounds, nextCommand.Bounds)
                    : default,
                HasBounds = mergedCommand.HasBounds && nextCommand.HasBounds
            };
            mergedCommandCount++;
            currentOrderIndex++;
        }

        return new ScheduledCommandMergeResult(mergedCommand, currentOrderIndex, mergedCommandCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UiRect MergeSchedulingBounds(UiRect a, UiRect b)
    {
        var x1 = MathF.Min(a.X, b.X);
        var y1 = MathF.Min(a.Y, b.Y);
        var x2 = MathF.Max(a.X + a.Width, b.X + b.Width);
        var y2 = MathF.Max(a.Y + a.Height, b.Y + b.Height);
        return new UiRect(x1, y1, x2 - x1, y2 - y1);
    }
}
