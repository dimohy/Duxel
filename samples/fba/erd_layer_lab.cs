// FBA: editable vector ERD, resident geometry, independent overview and demand rendering.
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;

Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", BenchOptions.ReadString("DUXEL_VK_PROFILE_EVERY", "1000000"));
using var measuredProcess = Process.GetCurrentProcess();
var processCpuStart = measuredProcess.TotalProcessorTime.TotalMilliseconds;
var processWallStart = Stopwatch.GetTimestamp();
var screen = new ErdLayerLab();
DuxelApp.Run(new DuxelAppOptions
{
    Window = new() { Title = "Duxel / ERD Layer Lab", Width = 1440, Height = 940, VSync = false },
    Renderer = new() { MsaaSamples = 4 },
    Frame = new()
    {
        EnableRenderPipeline = BenchOptions.ReadString("DUXEL_ERD_PIPELINE", "1") == "1",
        UseEventDrivenPlatformLoop = BenchOptions.ReadString("DUXEL_ERD_EVENT_LOOP", "1") == "1",
    },
    Font = new()
    {
        FontSize = 16,
        // Build the full atlas before composing the expensive retained scene.
        FastStartup = BenchOptions.ReadString("DUXEL_ERD_FAST_STARTUP", "0") == "1",
    },
    Debug = new() { LogStartupTimings = screen.TraceStartup },
    Screen = screen,
});
screen.SaveMeasurement(measuredProcess.TotalProcessorTime.TotalMilliseconds - processCpuStart,
    Stopwatch.GetElapsedTime(processWallStart).TotalMilliseconds, measuredProcess.PeakWorkingSet64);

public sealed class ErdLayerLab : UiScreen
{
    private const int TargetFps = 1000;
    private const int MeasurementCapacity = 1_048_576;
    private readonly bool _renderPipeline = BenchOptions.ReadString("DUXEL_ERD_PIPELINE", "1") == "1";
    private readonly bool _eventDrivenPlatformLoop = BenchOptions.ReadString("DUXEL_ERD_EVENT_LOOP", "1") == "1";
    private sealed class Table(string name, string[] columns, UiColor accent, Vector2 position, int index)
    {
        public readonly string Name = name;
        public readonly string[] Columns = columns;
        public readonly UiColor Accent = accent;
        public readonly string LayerId = "erd.table." + index;
        public readonly string MapId = "erd.map." + index;
        public readonly Vector2 GeometryOrigin = position;
        public Vector2 Position = position;
        public long Revision;
        public long DepthRank = index;
    }

    private readonly Table[] _tables;
    private readonly List<int> _stackOrder = [];
    private long _frontSequence;
    private readonly (int From, int To)[] _relations;
    private readonly string _introduction;
    private sealed class RelationBatch(int index)
    {
        public readonly string LayerId = "erd.relationships." + index;
        public long Revision;
    }
    private const int RelationsPerBatch = 32;
    private const float SelectedRelationThickness = 5f;
    private readonly RelationBatch[] _relationBatches;
    private readonly int[][] _tableRelationBatches;
    private readonly int[][] _tableRelations;
    private long _selectedRelationsRevision;
    private readonly int _tableCount = BenchOptions.ReadInt("DUXEL_ERD_TABLES", 1200, 12, 5000);
    private readonly bool _denseView = BenchOptions.ReadString("DUXEL_ERD_VIEW", "dense") == "dense";
    private float _worldWidth, _worldHeight, _fitZoom;
    private bool _cameraInitialized;
    private readonly Stopwatch _wall = new();
    private readonly BenchFrameRecorder? _frames;
    private readonly ulong[]? _recordedSourceFrameIds;
    private readonly long[]? _recordedWorkloadTimestamps;
    private readonly BenchWorkloadClock? _workloadClock;
    private long _firstMeasuredWorkloadTimestampTicks, _lastMeasuredWorkloadTimestampTicks;
    private long _lastMeasuredPresentationTimestampTicks;
    private float _workloadInitialZoom;
    private Vector2 _workloadInitialPan;
    private ulong _firstMeasuredPresentationSequence;
    private readonly string _output = BenchOptions.ReadString("DUXEL_ERD_OUT");
    private readonly bool _traceStartup = BenchOptions.ReadString("DUXEL_ERD_TRACE_STARTUP", "0") == "1";
    private readonly bool _traceViewport = BenchOptions.ReadString("DUXEL_ERD_TRACE_VIEWPORT", "0") == "1";
    private readonly bool _traceAllocations = BenchOptions.ReadString("DUXEL_ERD_TRACE_ALLOCATIONS", "0") == "1";
    internal bool TraceStartup => _traceStartup;
    private readonly string _scenario = BenchOptions.ReadString("DUXEL_ERD_SCENARIO", "interactive");
    private bool _fullRebuild = BenchOptions.ReadString("DUXEL_ERD_MODE", "resident") == "full";
    private readonly bool _directLayerReplay = BenchOptions.ReadString("DUXEL_ERD_DIRECT_REPLAY", "1") != "0";
    private IUiPresentationReader? _presentationReader;
    private readonly UiRendererFrameMetrics[] _presentationScratch = new UiRendererFrameMetrics[8];
    private readonly SourceTelemetry[] _sourceTelemetry = new SourceTelemetry[8];
    private int _nextSourceTelemetry;
    private readonly record struct SourceTelemetry(ulong FrameId, int Builds, long AllocatedBytes,
        bool WorkloadActive, long WorkloadTimestampTicks);
    private bool _autoCamera;
    private bool _animateTable;
    private float _zoom = .62f, _targetZoom = .62f;
    private Vector2 _pan, _targetPan;
    private Vector2 _focusVelocity;
    private float _zoomVelocity;
    private double _tourElapsed;
    private float _tourSpeed = (float)BenchOptions.ReadDouble("DUXEL_ERD_TOUR_SPEED", .45, .1, 1.2);
    private int _tourSeed;
    private long _lastTourCycle = -1;
    private readonly List<string> _tourVisits = [];
    private bool _anchoredZoom;
    private Vector2 _zoomAnchorWorld, _zoomAnchorScreen;
    private int _animatedIndex = -1;
    private Vector2 _motionVelocity, _motionOrigin, _motionExtent;
    private float _motionTime;
    private UiVector2 _previousMouse;
    private bool _panning;
    private bool _zoomDragging;
    private UiVector2 _zoomAnchor;
    private int _contextEntity = -1;
    private int _dragged = -1, _selected = 1;
    private UiVector2 _lastSize;
    private double _lastTime;
    private double _warmupUntil = double.PositiveInfinity;
    private long _warmupPresentationDeadlineTicks = long.MaxValue;
    private long _warmupPresentationStartTicks;
    private long _firstMeasuredPresentationTimestampTicks;
    private long _layoutRevision;
    private int _builds, _lastBuilds, _renderedFrames, _warmupFrame;
    private long _lastAllocated, _allocationSum, _buildSum;
    private ulong _uploadSum, _layerStateUploadSum, _compiledIndexUploadSum;
    private long _compiledDrawSum, _compiledCommandSum;
    private long _sharedBasisDrawSum, _sharedCameraDrawSum;
    private long _rendererAllocationSum;
    private long _cpuTimingCollectedFrames;
    private int _compiledIndexLimit;
    private bool _axisAlignedLayerCulling;
    private ulong _compiledIndexCachePeakBytes;
    private long _descriptorBuildSum;
    private long _commandVisitSum;
    private long _boundsVisitSum;
    private ulong _lastPresentationSequence, _warmupPresentationSequence;
    private int _rendererAllocationSamples;
    private int _dragStartFrame, _dragTwoSecondsFrame, _dragLastFrame, _dragMotionFrames, _measuredDragEntity = -1;
    private double _dragStartTime, _dragLastTime, _dragWorldDistance;
    private long _drawSum;
    private double _gpuSum;
    private int _gpuSamples;
    private const int StatusLineAdvanceIntervals = 16;
    private static readonly string StatusLineAdvanceShort = string.Join('\n', Enumerable.Repeat("Hg", StatusLineAdvanceIntervals + 1));
    private static readonly string StatusLineAdvanceLong = string.Join('\n', Enumerable.Repeat("Hg", 2 * StatusLineAdvanceIntervals + 1));
    private string _statusText = string.Empty;
    private string[] _statusLines = [];
    private static readonly string[] StatusLayerIds = ["erd.status.scene-builds", "erd.status.frame-cpu", "erd.status.gpu"];
    private readonly StatusLineLayer[] _statusLayers = new StatusLineLayer[3];

    private readonly record struct StatusLineLayer(string? Text, UiVector2 Position, UiColor Color, float LineAdvance, long Version);
    private double _nextStatusTime;
    private float _minimumZoom = float.PositiveInfinity, _maximumZoom;
    private float _maximumMotionSpeed;
    private readonly UiFpsCounter _fpsCounter = new(.5);
    private float _displayFps;
    private const float TableWidth = 260, TableHeight = 202;

    public ErdLayerLab()
    {
        if (_scenario is not ("interactive" or "camera" or "edit" or "idle" or "motion-tour"))
            throw new ArgumentException("Unknown DUXEL_ERD_SCENARIO.");
        var workloadTiming = BenchOptions.ReadString("DUXEL_ERD_WORKLOAD_CLOCK", "wall-total");
        if (workloadTiming == "post-warmup")
        {
            if (_output.Length == 0 || _scenario is "interactive" or "idle")
                throw new ArgumentException("Post-warmup workload timing requires an output and an active benchmark scenario.");
            var secondsText = BenchOptions.ReadString("DUXEL_ERD_MEASURE_SECONDS", "6");
            if (!double.TryParse(secondsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                || !double.IsFinite(seconds) || seconds <= 0 || seconds > 60)
                throw new ArgumentException("DUXEL_ERD_MEASURE_SECONDS must be finite, positive and at most60.");
            _workloadClock = new(Stopwatch.Frequency, seconds);
        }
        else if (workloadTiming != "wall-total") throw new ArgumentException("Unknown DUXEL_ERD_WORKLOAD_CLOCK.");
        if (_output.Length != 0)
        {
            _frames = new(MeasurementCapacity);
            _recordedSourceFrameIds = new ulong[MeasurementCapacity];
            if (_workloadClock is not null) _recordedWorkloadTimestamps = new long[MeasurementCapacity];
        }
        string[] names = ["customers", "orders", "products", "inventory", "addresses", "order_items", "categories", "suppliers", "payments", "shipments", "warehouses", "stock_events"];
        string[][] fields =
        [
            ["PK customer_id : uuid", "name : varchar(120)", "email : varchar(255)", "created_at : timestamp"],
            ["PK order_id : uuid", "FK customer_id : uuid", "status : order_status", "total : decimal(12,2)"],
            ["PK product_id : uuid", "FK category_id : uuid", "name : varchar(160)", "price : decimal(12,2)"],
            ["PK inventory_id : uuid", "FK product_id : uuid", "FK warehouse_id : uuid", "quantity : integer"],
            ["PK address_id : uuid", "FK customer_id : uuid", "city : varchar(80)", "postal_code : varchar(20)"],
            ["PK item_id : uuid", "FK order_id : uuid", "FK product_id : uuid", "quantity : integer"],
            ["PK category_id : uuid", "name : varchar(80)", "parent_id : uuid", "sort_order : integer"],
            ["PK supplier_id : uuid", "name : varchar(160)", "contact : varchar(255)", "country : char(2)"],
            ["PK payment_id : uuid", "FK order_id : uuid", "amount : decimal(12,2)", "paid_at : timestamp"],
            ["PK shipment_id : uuid", "FK order_id : uuid", "tracking : varchar(80)", "shipped_at : timestamp"],
            ["PK warehouse_id : uuid", "name : varchar(80)", "region : varchar(80)", "capacity : integer"],
            ["PK event_id : uuid", "FK inventory_id : uuid", "delta : integer", "recorded_at : timestamp"],
        ];
        UiColor[] colors = [new(0xFFBFA65E), new(0xFFE3AF64), new(0xFFCB8CBB), new(0xFF79BDA0)];
        _tables = new Table[_tableCount];
        var columns = _tableCount == 12 ? 4 : (int)Math.Ceiling(Math.Sqrt(_tableCount * 1.4));
        var rows = (_tableCount + columns - 1) / columns;
        _worldWidth = columns * 340 - 80;
        _worldHeight = rows * 270 - 68;
        for (var i = 0; i < _tables.Length; i++)
        {
            var template = i % names.Length;
            var name = _tableCount == 12 ? names[template] : names[template] + "_" + i;
            _tables[i] = new(name, fields[template], colors[i % colors.Length],
                new(i % columns * 340 - _worldWidth / 2, i / columns * 270 - _worldHeight / 2), i);
            _stackOrder.Add(i);
        }
        _relations = new (int From, int To)[_tableCount * 2];
        _introduction = $"COMMERCE DATABASE\n{_tables.Length} tables / {_relations.Length} links\nVector text + vector shapes";
        for (var i = 0; i < _tableCount; i++)
        {
            _relations[i * 2] = (i, (i + 1) % _tableCount);
            _relations[i * 2 + 1] = (i, (i + columns) % _tableCount);
        }
        _relationBatches = new RelationBatch[(_relations.Length + RelationsPerBatch - 1) / RelationsPerBatch];
        for (var i = 0; i < _relationBatches.Length; i++) _relationBatches[i] = new(i);
        var dependencies = new List<int>[_tables.Length];
        var tableRelations = new List<int>[_tables.Length];
        for (var i = 0; i < dependencies.Length; i++) { dependencies[i] = []; tableRelations[i] = []; }
        for (var i = 0; i < _relations.Length; i++)
        {
            var batch = i / RelationsPerBatch;
            var (from, to) = _relations[i];
            if (!dependencies[from].Contains(batch)) dependencies[from].Add(batch);
            if (!dependencies[to].Contains(batch)) dependencies[to].Add(batch);
            tableRelations[from].Add(i);
            if (to != from) tableRelations[to].Add(i);
        }
        _tableRelationBatches = new int[_tables.Length][];
        for (var i = 0; i < dependencies.Length; i++) _tableRelationBatches[i] = dependencies[i].ToArray();
        _tableRelations = new int[_tables.Length][];
        for (var i = 0; i < tableRelations.Length; i++) _tableRelations[i] = tableRelations[i].ToArray();
        _frontSequence = _tables.Length;
        SelectAndRaise(_selected);
        _autoCamera = _scenario is "interactive" or "camera" or "motion-tour";
        _animateTable = _scenario is "interactive" or "edit" or "motion-tour";
    }

    public override void Render(UiImmediateContext ui)
    {
        var traceAllocations = _traceAllocations && (_renderedFrames + 1) % 300 == 0;
        var frameAllocatedBefore = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        var now = ui.GetTime();
        if (!_wall.IsRunning) _wall.Start();
        var delta = _lastTime == 0 ? .016 : Math.Clamp(now - _lastTime, 0, .05);
        var frameDelta = now - _lastTime;
        _lastTime = now;
        _renderedFrames++;
        var metrics = ui.GetRendererFrameMetrics();
        _presentationReader ??= ui.GetPresentationReader();
        CollectPresentedFrames();
        var viewport = ui.GetMainViewport();
        var fullSize = new UiVector2(viewport.Pos.X + viewport.Size.X, viewport.Pos.Y + viewport.Size.Y);
        var viewportChanged = fullSize != _lastSize;
        if (viewportChanged) { _lastSize = fullSize; _layoutRevision++; }
        var viewportTraceStart = _traceViewport && viewportChanged ? Stopwatch.GetTimestamp() : 0;
        var board = new UiRect(viewport.WorkPos.X + 274, viewport.WorkPos.Y + 56, viewport.WorkSize.X - 290, viewport.WorkSize.Y - 76);
        _fitZoom = MathF.Min(board.Width / _worldWidth, board.Height / _worldHeight) * .9f;
        if (!_cameraInitialized) { _zoom = _targetZoom = _denseView ? _fitZoom : .62f; _cameraInitialized = true; }
        var map = new UiRect(viewport.WorkPos.X + 24, viewport.WorkPos.Y + viewport.WorkSize.Y - 170, 216, 146);
        var mouse = ui.GetMousePos();
        var workloadStep = default(BenchWorkloadStep);
        if (_workloadClock is { } workloadClock)
        {
            var wasStarted = workloadClock.Started;
            workloadStep = workloadClock.Advance(Stopwatch.GetTimestamp(), _warmupPresentationDeadlineTicks);
            if (workloadStep.Complete) { DuxelApp.Exit(); return; }
            // Preparation builds and uploads the real scene at its initial pose.
            // Simulation time begins only once the presentation warmup is ready.
            frameDelta = workloadStep.DeltaSeconds;
            delta = Math.Clamp(frameDelta, 0, .05);
            if (!wasStarted && workloadStep.Running)
            {
                _workloadInitialZoom = _zoom;
                _workloadInitialPan = _pan;
            }
        }
        if (_scenario == "interactive") HandleInput(ui, board, mouse);
        if (_animateTable)
        {
            if (_animatedIndex < 0) StartTableMotion(board);
            UpdateTableMotion((float)delta);
        }
        if (_autoCamera) UpdateTour(board, (float)Math.Max(0, frameDelta));
        UpdateCamera((float)delta);
        _minimumZoom = MathF.Min(_minimumZoom, _zoom);
        _maximumZoom = MathF.Max(_maximumZoom, _zoom);
        var scene = Scene(board, _zoom, _pan, .4f);
        var draw = ui.GetWindowDrawList();
        draw.AddRectFilledRounded(board, new(0xFF201B17), ui.WhiteTextureId, 10, board);
        var buildsBefore = _builds;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        DrawGrid(ui, board, scene);
        var afterGrid = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        DrawRelations(ui, board, scene with { Transform = scene.Transform * Matrix4x4.CreateTranslation(0, 0, .1f) });
        var afterRelations = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        for (var i = 0; i < _tables.Length; i++) DrawTable(ui, board, scene, i);
        var afterTables = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        DrawSelectedRelations(ui, board, scene);
        var afterSelectedRelations = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        DrawOverview(ui, map, board);
        var afterOverview = GC.GetAllocatedBytesForCurrentThread();
        _lastAllocated = afterOverview - allocatedBefore;
        _lastBuilds = _builds - buildsBefore;
        var sourceFrameId = ui.GetDrawDataFrameId();
        if (viewportTraceStart != 0)
        {
            var completed = Stopwatch.GetTimestamp();
            var gc = GC.GetGCMemoryInfo();
            Console.WriteLine(FormattableString.Invariant(
                $"[erd-viewport] frame={_renderedFrames} source={sourceFrameId} revision={_layoutRevision} startTicks={viewportTraceStart} completedTicks={completed} frequency={Stopwatch.Frequency} viewport={viewport.Pos.X},{viewport.Pos.Y},{viewport.Size.X},{viewport.Size.Y} work={viewport.WorkPos.X},{viewport.WorkPos.Y},{viewport.WorkSize.X},{viewport.WorkSize.Y} builds={_lastBuilds} sceneAllocated={_lastAllocated} gcCommitted={gc.TotalCommittedBytes} gcGen2={GC.CollectionCount(2)}"));
        }
        if (_traceStartup && _renderedFrames <= 8)
        {
            Console.WriteLine(FormattableString.Invariant(
                $"[erd-startup] frame={_renderedFrames} source={sourceFrameId} viewport={viewport.Pos.X},{viewport.Pos.Y},{viewport.Size.X},{viewport.Size.Y} work={viewport.WorkPos.X},{viewport.WorkPos.Y},{viewport.WorkSize.X},{viewport.WorkSize.Y} revision={_layoutRevision} builds={_lastBuilds} sceneAllocated={_lastAllocated}"));
        }
        RecordSourceTelemetry(ui, _lastBuilds, _lastAllocated,
            _workloadClock is null || workloadStep.Running, workloadStep.TimestampTicks);
        var diagramUsesDirectText = ui.GetDirectTextEnabled();
        var panelAllocatedBefore = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        ui.SetDirectTextEnabled(true);
        try
        {
            DrawControls(ui, viewport.WorkPos, board, metrics);
        }
        finally
        {
            ui.SetDirectTextEnabled(diagramUsesDirectText);
        }
        var panelAllocated = traceAllocations ? GC.GetAllocatedBytesForCurrentThread() - panelAllocatedBefore : 0;
        DrawContextMenu(ui);
        _previousMouse = mouse;
        if (_fullRebuild || _autoCamera || _animateTable || _wall.Elapsed.TotalSeconds < _warmupUntil
            || MathF.Abs(_zoom - _targetZoom) > .0001f || Vector2.DistanceSquared(_pan, _targetPan) > .001f
            || _focusVelocity.LengthSquared() > .001f || MathF.Abs(_zoomVelocity) > .001f)
            DuxelApp.RequestFrame();
        if (traceAllocations)
        {
            var sampleAllocated = GC.GetAllocatedBytesForCurrentThread() - frameAllocatedBefore;
            Console.WriteLine(FormattableString.Invariant(
                $"[erd-allocations] frame={_renderedFrames} source={sourceFrameId} screen={sampleAllocated} scene={_lastAllocated} panel={panelAllocated} other={sampleAllocated - _lastAllocated - panelAllocated} grid={afterGrid - allocatedBefore} relations={afterRelations - afterGrid} tables={afterTables - afterRelations} selectedRelations={afterSelectedRelations - afterTables} overview={afterOverview - afterSelectedRelations}"));
        }
    }

    private void RecordSourceTelemetry(UiImmediateContext ui, int builds, long allocatedBytes, bool workloadActive, long workloadTimestamp)
    {
        if (!ui.IsPresentationPass)
            return;

        // Input-only passes consume source IDs without producing draw data.
        // Index by recording order so a gap in IDs cannot overwrite a pending presentation.
        _sourceTelemetry[_nextSourceTelemetry] = new(ui.GetDrawDataFrameId(), builds, allocatedBytes, workloadActive, workloadTimestamp);
        _nextSourceTelemetry = (_nextSourceTelemetry + 1) % _sourceTelemetry.Length;
    }

    private SourceTelemetry FindSourceTelemetry(ulong sourceFrameId)
    {
        if (sourceFrameId != 0)
        {
            foreach (var telemetry in _sourceTelemetry)
            {
                if (telemetry.FrameId == sourceFrameId)
                    return telemetry;
            }
        }

        throw new InvalidOperationException($"ERD_SOURCE_TELEMETRY_MISSING: presentation source {sourceFrameId} is not in the retained UI frames.");
    }

    private void CollectPresentedFrames()
    {
        if (_presentationReader is null) return;
        if (_presentationReader.OverwrittenSamples != 0)
            throw new InvalidOperationException("ERD_PRESENTATION_HISTORY_OVERFLOW: timing samples were overwritten.");
        int count;
        while ((count = _presentationReader.Drain(_presentationScratch)) > 0)
        {
            for (var sample = 0; sample < count; sample++)
            {
                var metrics = _presentationScratch[sample];
                if (metrics.PresentationSequence != _lastPresentationSequence + 1)
                    throw new InvalidOperationException("ERD_PRESENTATION_SEQUENCE_GAP: another consumer removed timing samples.");
                _lastPresentationSequence = metrics.PresentationSequence;
                var telemetry = FindSourceTelemetry(metrics.DrawDataFrameId);
                var presentationDelta = metrics.PresentationIntervalSeconds;
                if (metrics.PresentationSequence == 1)
                {
                    _warmupPresentationStartTicks = metrics.PresentationTimestampTicks;
                    _warmupPresentationDeadlineTicks = checked(_warmupPresentationStartTicks + Stopwatch.Frequency);
                    _warmupUntil = _wall.Elapsed.TotalSeconds
                        + Math.Max(0, (_warmupPresentationDeadlineTicks - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
                }
                if (presentationDelta > 0) _displayFps = _fpsCounter.Tick(presentationDelta).Fps;
                if (metrics.PresentationTimestampTicks < _warmupPresentationDeadlineTicks) continue;
                if (_workloadClock is { } workloadClock
                    && (!telemetry.WorkloadActive || !workloadClock.Contains(telemetry.WorkloadTimestampTicks))) continue;
                if (_warmupFrame == 0)
                {
                    _warmupFrame = _renderedFrames;
                    _warmupPresentationSequence = metrics.PresentationSequence - 1;
                }
                if (_scenario == "idle" || metrics.PresentationSequence == 1) continue;
                if (_frames is not { } frames) continue;
                if (presentationDelta <= 0) throw new InvalidOperationException("ERD_INVALID_PRESENTATION_INTERVAL");
                if (frames.Count == 0)
                {
                    _firstMeasuredPresentationSequence = metrics.PresentationSequence;
                    _firstMeasuredPresentationTimestampTicks = metrics.PresentationTimestampTicks;
                }
                var sampleIndex = frames.Count;
                frames.Record(presentationDelta);
                _recordedSourceFrameIds![sampleIndex] = metrics.DrawDataFrameId;
                if (_recordedWorkloadTimestamps is { } workloadTimestamps)
                {
                    workloadTimestamps[sampleIndex] = telemetry.WorkloadTimestampTicks;
                    if (sampleIndex == 0) _firstMeasuredWorkloadTimestampTicks = telemetry.WorkloadTimestampTicks;
                    _lastMeasuredWorkloadTimestampTicks = telemetry.WorkloadTimestampTicks;
                }
                _lastMeasuredPresentationTimestampTicks = metrics.PresentationTimestampTicks;
                if (metrics.CpuTimingCollected) _cpuTimingCollectedFrames++;
                _compiledIndexLimit = metrics.CompiledIndexLimit;
                _axisAlignedLayerCulling = metrics.AxisAlignedLayerCulling;
                _compiledIndexCachePeakBytes = Math.Max(_compiledIndexCachePeakBytes, metrics.CompiledIndexCacheBytes);
                _buildSum += telemetry.Builds;
                _allocationSum += telemetry.AllocatedBytes;
                if (metrics.RendererAllocatedBytes is { } rendererAllocation)
                {
                    _rendererAllocationSum += rendererAllocation;
                    _rendererAllocationSamples++;
                }
                _uploadSum += metrics.DynamicUploadBytes + metrics.StagingUploadBytes + metrics.CompiledIndexUploadBytes + metrics.LayerStateUploadBytes;
                _layerStateUploadSum += metrics.LayerStateUploadBytes;
                _compiledIndexUploadSum += metrics.CompiledIndexUploadBytes;
                _compiledDrawSum += metrics.CompiledLayerDraws;
                _sharedBasisDrawSum += metrics.SharedBasisLayerDraws;
                _sharedCameraDrawSum += metrics.SharedCameraLayerDraws;
                _compiledCommandSum += metrics.CompiledLayerCommands;
                _descriptorBuildSum += metrics.CompiledDescriptorBuilds;
                _commandVisitSum += metrics.CompiledCommandVisits;
                _boundsVisitSum += metrics.LayerBoundsCommandVisits;
                _drawSum += metrics.DrawCalls;
                if (double.IsFinite(metrics.GpuRenderMicroseconds)) { _gpuSum += metrics.GpuRenderMicroseconds; _gpuSamples++; }
            }
        }
    }

    private void UpdateTour(UiRect board, float delta)
    {
        _tourElapsed += delta * _tourSpeed;
        const double cycleSeconds = 6.4;
        var cycle = (long)(_tourElapsed / cycleSeconds);
        var phase = _tourElapsed % cycleSeconds;
        var followsMoving = _animateTable && _animatedIndex >= 0 && (cycle & 1) == 0;
        var index = followsMoving ? _animatedIndex : PickTourEntity(cycle);
        if (cycle != _lastTourCycle)
        {
            _lastTourCycle = cycle;
            if (_tourVisits.Count == 32) _tourVisits.RemoveAt(0);
            _tourVisits.Add(string.Create(CultureInfo.InvariantCulture,
                $"{{\"cycle\":{cycle},\"entity\":{index},\"followsMoving\":{(followsMoving ? "true" : "false")}}}"));
        }
        var closeFocus = _tables[index].Position + new Vector2(TableWidth / 2, TableHeight / 2);
        var closeZoom = Math.Clamp(MathF.Min(board.Width / (TableWidth * 2.6f), board.Height / (TableHeight * 2.6f)), .8f, 1.2f);
        var wideZoom = _fitZoom * .85f;
        float amount;
        Vector2 focus;
        if (phase < .8)
        {
            amount = 0;
            focus = Vector2.Lerp(Vector2.Zero, closeFocus, EaseMotion((float)(phase / .8)));
        }
        else if (phase < 2.4)
        {
            amount = EaseMotion((float)((phase - .8) / 1.6));
            focus = closeFocus;
        }
        else if (phase < 3.7)
        {
            amount = 1;
            focus = closeFocus;
        }
        else if (phase < 5.3)
        {
            amount = 1 - EaseMotion((float)((phase - 3.7) / 1.6));
            focus = closeFocus;
        }
        else
        {
            amount = 0;
            focus = Vector2.Lerp(closeFocus, Vector2.Zero, EaseMotion((float)((phase - 5.3) / 1.1)));
        }
        focus += new Vector2(MathF.Sin((float)_tourElapsed * .8f) * TableWidth * .35f,
            MathF.Sin((float)_tourElapsed * .6f) * TableHeight * .3f) * amount;
        _targetZoom = wideZoom + (closeZoom - wideZoom) * amount;
        _targetPan = -focus * _targetZoom;
        _anchoredZoom = false;
    }

    // Alternate tracking with visits across the document.
    private int PickTourEntity(long cycle)
    {
        // Reproducible pseudo-random visits make camera benchmarks repeatable.
        var hash = unchecked((uint)cycle * 747796405u + (uint)_tourSeed * 2891336453u + 277803737u);
        hash = (hash ^ (hash >> 16)) * 2246822519u;
        hash = (hash ^ (hash >> 13)) * 3266489917u;
        hash ^= hash >> 16;
        var index = (int)(hash % (uint)_tables.Length);
        return index == _animatedIndex ? (index + 1) % _tables.Length : index;
    }

    // C2-continuous start and stop: velocity and acceleration vanish at both ends.
    private static float EaseMotion(float value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private void UpdateCamera(float delta)
    {
        if (_autoCamera)
        {
            _zoom = _targetZoom;
            _pan = _targetPan;
            _zoomVelocity = 0;
            _focusVelocity = default;
            return;
        }
        var frequency = _autoCamera ? 7.5f : 14f;
        var decay = MathF.Exp(-frequency * delta);
        var logZoom = MathF.Log(_zoom);
        var targetLogZoom = MathF.Log(_targetZoom);
        var zoomError = logZoom - targetLogZoom;
        var zoomStep = (_zoomVelocity + frequency * zoomError) * delta;
        _zoomVelocity = (_zoomVelocity - frequency * zoomStep) * decay;
        _zoom = MathF.Exp(targetLogZoom + (zoomError + zoomStep) * decay);
        if (_anchoredZoom)
        {
            _pan = _zoomAnchorScreen - _zoomAnchorWorld * _zoom;
            _targetPan = _zoomAnchorScreen - _zoomAnchorWorld * _targetZoom;
            _focusVelocity = default;
            if (MathF.Abs(zoomError) < .0001f && MathF.Abs(_zoomVelocity) < .001f) _anchoredZoom = false;
        }
        else
        {
            var focus = -_pan / MathF.Exp(logZoom);
            var targetFocus = -_targetPan / _targetZoom;
            var error = focus - targetFocus;
            var step = (_focusVelocity + frequency * error) * delta;
            _focusVelocity = (_focusVelocity - frequency * step) * decay;
            focus = targetFocus + (error + step) * decay;
            _pan = -focus * _zoom;
        }
    }

    private void StartTableMotion(UiRect board)
    {
        _animatedIndex = _selected;
        SelectAndRaise(_animatedIndex);
        _motionOrigin = _tables[_animatedIndex].Position;
        _motionExtent = new(MathF.Max(180, board.Width / _zoom * .24f), MathF.Max(140, board.Height / _zoom * .24f));
        _motionVelocity = default;
        _motionTime = 0;
    }

    private void UpdateTableMotion(float delta)
    {
        _motionTime += delta * .65f;
        var maximumSpeed = MathF.Max(180, MathF.Min(_motionExtent.X, _motionExtent.Y) * .35f);
        var acceleration = new Vector2(MathF.Sin(_motionTime * 1.7f) + .6f * MathF.Cos(_motionTime * .73f),
            MathF.Cos(_motionTime * 1.37f) - .5f * MathF.Sin(_motionTime * 2.31f)) * maximumSpeed * .9f;
        _motionVelocity = (_motionVelocity + acceleration * delta) * MathF.Exp(-.16f * delta);
        if (_motionVelocity.LengthSquared() > maximumSpeed * maximumSpeed)
            _motionVelocity = Vector2.Normalize(_motionVelocity) * maximumSpeed;
        var position = _tables[_animatedIndex].Position + _motionVelocity * delta;
        var minimum = _motionOrigin - _motionExtent;
        var maximum = _motionOrigin + _motionExtent;
        if (position.X < minimum.X || position.X > maximum.X) { position.X = Math.Clamp(position.X, minimum.X, maximum.X); _motionVelocity.X *= -.8f; }
        if (position.Y < minimum.Y || position.Y > maximum.Y) { position.Y = Math.Clamp(position.Y, minimum.Y, maximum.Y); _motionVelocity.Y *= -.8f; }
        _tables[_animatedIndex].Position = position;
        _maximumMotionSpeed = MathF.Max(_maximumMotionSpeed, _motionVelocity.Length());
        InvalidateRelations(_animatedIndex);
    }

    private void InvalidateRelations(int tableIndex)
    {
        // All edges incident to the selection live in the foreground layer.
        // Moving the selected table does not change any background relationship.
        if (tableIndex != _selected) InvalidateRelationBatches(tableIndex);
        foreach (var relation in _tableRelations[tableIndex])
        {
            var (from, to) = _relations[relation];
            if (from != _selected && to != _selected) continue;
            _selectedRelationsRevision++;
            break;
        }
    }

    private void InvalidateRelationBatches(int tableIndex)
    {
        foreach (var batch in _tableRelationBatches[tableIndex]) _relationBatches[batch].Revision++;
    }

    private static UiLayer3D Scene(UiRect viewport, float zoom, Vector2 pan, float depth)
        => new(Matrix4x4.CreateScale(zoom, zoom, 1) * Matrix4x4.CreateTranslation(pan.X * 2 / viewport.Width, pan.Y * 2 / viewport.Height, depth))
        { Viewport = viewport, PlaneModel = new(default) };
    private UiLayerOptions Options(UiLayer3D scene, long revision, bool fontAtlasIndependent = true)
        => new(false, 1, default)
        {
            Scene3D = scene,
            ContentVersion = _fullRebuild ? null : revision + _layoutRevision * 1_000_000_000L,
            FontAtlasIndependent = fontAtlasIndependent,
        };
    private static UiVector2 Origin(UiRect view, Vector2 point) => new(view.X + view.Width * .5f + point.X, view.Y + view.Height * .5f + point.Y);
    private static bool Contains(UiRect rect, UiVector2 point) => point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    private void HandleInput(UiImmediateContext ui, UiRect board, UiVector2 mouse)
    {
        if (ui.IsMouseClicked(1) && Contains(board, mouse))
        {
            _panning = _zoomDragging = false;
            _autoCamera = false;
            _dragged = -1;
            _contextEntity = HitEntity(board, mouse);
            if (_contextEntity >= 0) SelectAndRaise(_contextEntity);
            ui.OpenPopup("erd.context");
        }
        if (ui.IsPopupOpen("erd.context")) return;
        var middleDown = ui.IsMouseDown(2);
        if (Contains(board, mouse) && ui.IsMouseClicked(2))
        {
            _zoomDragging = (ui.GetKeyModifiers() & KeyModifiers.Ctrl) != 0;
            _panning = !_zoomDragging;
            _zoomAnchor = mouse;
            _previousMouse = mouse;
            _autoCamera = false;
            _dragged = -1;
            if (_panning) { _anchoredZoom = false; _targetZoom = _zoom; _targetPan = _pan; _zoomVelocity = 0; }
        }
        if (_panning && middleDown)
            _targetPan += new Vector2(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y);
        if (_zoomDragging && middleDown)
            ZoomAt(board, _zoomAnchor, MathF.Exp(-.012f * (mouse.Y - _previousMouse.Y)));
        if (!middleDown) _panning = _zoomDragging = false;
        if (Contains(board, mouse))
        {
            var wheel = ui.GetMouseWheel();
            if (wheel != 0)
            {
                _autoCamera = false;
                ZoomAt(board, mouse, MathF.Exp(wheel * .14f));
            }
            if (!_panning && !_zoomDragging && ui.IsMouseClicked(0))
            {
                var entity = HitEntity(board, mouse);
                if (entity >= 0)
                {
                    _autoCamera = false;
                    SelectAndRaise(entity);
                    _dragged = entity;
                    if (_output.Length != 0)
                    {
                        _measuredDragEntity = entity;
                        _dragStartFrame = _dragTwoSecondsFrame = _dragLastFrame = _renderedFrames;
                        _dragStartTime = _dragLastTime = ui.GetTime();
                        _dragMotionFrames = 0;
                        _dragWorldDistance = 0;
                    }
                    _previousMouse = mouse;
                }
            }
        }
        if (_dragged >= 0 && !_panning && !_zoomDragging && ui.IsMouseDown(0))
        {
            if (_output.Length != 0)
            {
                _dragLastFrame = _renderedFrames;
                _dragLastTime = ui.GetTime();
                if (_dragLastTime - _dragStartTime <= 2) _dragTwoSecondsFrame = _renderedFrames;
            }
            var motion = new Vector2(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y) / _zoom;
            if (motion.LengthSquared() > .0001f)
            {
                _tables[_dragged].Position += motion; InvalidateRelations(_dragged);
                if (_output.Length != 0) { _dragMotionFrames++; _dragWorldDistance += motion.Length(); }
            }
        }
        if (!ui.IsMouseDown(0)) _dragged = -1;
    }

    private void ZoomAt(UiRect board, UiVector2 anchor, float factor)
    {
        var nextZoom = Math.Clamp(_targetZoom * factor, .015f, 6f);
        var relative = new Vector2(anchor.X - board.X - board.Width * .5f, anchor.Y - board.Y - board.Height * .5f);
        if (!_anchoredZoom || _zoomAnchorScreen != relative)
            _zoomAnchorWorld = (relative - _pan) / _zoom;
        _zoomAnchorScreen = relative;
        _anchoredZoom = true;
        _targetPan = relative - _zoomAnchorWorld * nextZoom;
        _targetZoom = nextZoom;
    }

    private int HitEntity(UiRect board, UiVector2 mouse)
    {
        if (!Scene(board, _zoom, _pan, .4f).TryMapScreenPoint(mouse, new(0, 0, _lastSize.X, _lastSize.Y), out var local)) return -1;
        for (var stackIndex = _stackOrder.Count - 1; stackIndex >= 0; stackIndex--)
        {
            var index = _stackOrder[stackIndex];
            var origin = Origin(board, _tables[index].Position);
            if (Contains(new(origin.X, origin.Y, TableWidth, TableHeight), local)) return index;
        }
        return -1;
    }

    private void SelectAndRaise(int index)
    {
        if (_selected != index)
        {
            _tables[_selected].Revision++;
            InvalidateRelationBatches(_selected);
            _tables[index].Revision++;
            InvalidateRelationBatches(index);
            _selectedRelationsRevision++;
        }
        _selected = index;
        _tables[index].DepthRank = ++_frontSequence;
        _stackOrder.Remove(index);
        _stackOrder.Add(index);
    }

    private void DrawContextMenu(UiImmediateContext ui)
    {
        if (!ui.BeginPopup("erd.context")) return;
        ui.Text(_contextEntity >= 0 ? _tables[_contextEntity].Name : "Canvas actions");
        if (ui.MenuItem("Bring entity to front", enabled: _contextEntity >= 0))
        {
            SelectAndRaise(_contextEntity);
            ui.CloseCurrentPopup(); DuxelApp.RequestFrame();
        }
        if (ui.MenuItem("Frame selected entity", enabled: _contextEntity >= 0))
        {
            _targetZoom = 1.2f;
            _targetPan = -(_tables[_contextEntity].Position + new Vector2(TableWidth / 2, TableHeight / 2)) * _targetZoom;
            ui.CloseCurrentPopup(); DuxelApp.RequestFrame();
        }
        if (ui.MenuItem("Fit entire diagram"))
        {
            _targetZoom = _fitZoom; _targetPan = default;
            ui.CloseCurrentPopup(); DuxelApp.RequestFrame();
        }
        ui.EndPopup();
    }

    private void DrawGrid(UiImmediateContext ui, UiRect board, UiLayer3D scene)
    {
        var options = Options(scene with { PlaneModel = new(default, .2f) }, 0);
        if (_directLayerReplay && ui.TryReplayLayer("erd.grid", options)) return;
        if (ui.BeginLayer("erd.grid", options))
        {
            _builds++;
            var draw = ui.GetWindowDrawList();
            for (var x = -1200; x <= 1200; x += 40)
            for (var y = -800; y <= 800; y += 40)
            {
                var p = Origin(board, new(x, y));
                draw.AddCircleFilled(p, 1, new(0xFF493D32), ui.WhiteTextureId);
            }
        }
        ui.EndLayer();
    }

    private void DrawRelations(UiImmediateContext ui, UiRect board, UiLayer3D scene)
    {
        for (var batchIndex = 0; batchIndex < _relationBatches.Length; batchIndex++)
        {
            var batch = _relationBatches[batchIndex];
            var options = Options(scene, batch.Revision);
            if (_directLayerReplay && ui.TryReplayLayer(batch.LayerId, options)) continue;
            if (ui.BeginLayer(batch.LayerId, options))
            {
                _builds++;
                var draw = ui.GetWindowDrawList();
                var end = Math.Min(_relations.Length, (batchIndex + 1) * RelationsPerBatch);
                for (var relationIndex = batchIndex * RelationsPerBatch; relationIndex < end; relationIndex++)
                {
                    var (from, to) = _relations[relationIndex];
                    if (from == _selected || to == _selected) continue;
                    DrawRelation(ui, draw, board, relationIndex, highlighted: false);
                }
            }
            ui.EndLayer();
        }
    }

    private void DrawSelectedRelations(UiImmediateContext ui, UiRect board, UiLayer3D scene)
    {
        // A depth between the selected table and every other table keeps its
        // connectors in front without painting over the selected table's text.
        var depth = -.2f * (float)((_tables[_selected].DepthRank - .5d) / _frontSequence);
        var foreground = scene with
        {
            PlaneModel = new(default, depth),
            WriteDepth = false,
        };
        var options = Options(foreground, _selectedRelationsRevision);
        if (_directLayerReplay && ui.TryReplayLayer("erd.selected-relationships", options)) return;
        if (ui.BeginLayer("erd.selected-relationships", options))
        {
            _builds++;
            var draw = ui.GetWindowDrawList();
            foreach (var relation in _tableRelations[_selected]) DrawRelation(ui, draw, board, relation, highlighted: true);
        }
        ui.EndLayer();
    }

    private void DrawRelation(UiImmediateContext ui, UiDrawListBuilder draw, UiRect board, int relationIndex, bool highlighted)
    {
        var (from, to) = _relations[relationIndex];
        var a = Origin(board, _tables[from].Position + new Vector2(TableWidth, 98));
        var b = Origin(board, _tables[to].Position + new Vector2(0, 98));
        var midX = (a.X + b.X) * .5f;
        var color = highlighted ? _tables[_selected].Accent : new UiColor(0xFF97775A);
        var thickness = highlighted ? SelectedRelationThickness : 2f;
        draw.AddLine(a, new(midX, a.Y), color, thickness);
        draw.AddLine(new(midX, a.Y), new(midX, b.Y), color, thickness);
        draw.AddLine(new(midX, b.Y), b, color, thickness);
        draw.AddCircleFilled(a, highlighted ? 5.5f : 4, highlighted ? color : _tables[from].Accent, ui.WhiteTextureId);
        draw.AddCircleFilled(b, highlighted ? 5.5f : 4, highlighted ? color : _tables[to].Accent, ui.WhiteTextureId);
    }

    private void DrawTable(UiImmediateContext ui, UiRect board, UiLayer3D scene, int index)
    {
        var table = _tables[index];
        var movement = table.Position - table.GeometryOrigin;
        scene = scene with { PlaneModel = new(new(movement.X, movement.Y),
            -.2f * (float)((double)table.DepthRank / _frontSequence)) };
        var options = Options(scene, table.Revision, fontAtlasIndependent: ui.GetDirectTextEnabled());
        if (_directLayerReplay && ui.TryReplayLayer(table.LayerId, options)) return;

        var origin = Origin(board, table.GeometryOrigin);
        var bounds = new UiRect(origin.X, origin.Y, TableWidth, TableHeight);
        if (scene.IsPlaneOutsideViewport(bounds, board)) return;

        if (ui.BeginLayer(table.LayerId, options))
        {
            _builds++;
            var draw = ui.GetWindowDrawList();
            draw.PushClipRect(bounds);
            draw.AddRectFilledRounded(bounds, new(0xFF352C25), index == _selected ? new(0xFFFFFFFF) : new(0xFF655140), ui.WhiteTextureId, 10, index == _selected ? 2 : 1, draw.GetClipRect());
            var title = new UiRect(origin.X + 2, origin.Y + 2, TableWidth - 4, 43);
            // Clip the extended rounded fill before its lower corners begin.
            draw.AddRectFilledRounded(new(title.X, title.Y, title.Width, title.Height + 8), table.Accent, ui.WhiteTextureId, 8, title);
            ui.SetCursorScreenPos(new(origin.X + 14, origin.Y + 10));
            ui.TextColored(new(0xFF201A16), table.Name);
            for (var column = 0; column < table.Columns.Length; column++)
            {
                ui.SetCursorScreenPos(new(origin.X + 14, origin.Y + 58 + column * 30));
                ui.TextColored(column == 0 ? table.Accent : new(0xFFD8CDC0), table.Columns[column]);
            }
            draw.PopClipRect();
        }
        ui.EndLayer();
    }

    private void DrawOverview(UiImmediateContext ui, UiRect map, UiRect board)
    {
        ui.GetWindowDrawList().AddRectFilledRounded(map, new(0xFF201B17), ui.WhiteTextureId, 8, map);
        var scene = Scene(map, MathF.Min(map.Width / _worldWidth, map.Height / _worldHeight) * .9f, default, .4f);
        for (var i = 0; i < _tables.Length; i++)
        {
            var table = _tables[i];
            var movement = table.Position - table.GeometryOrigin;
            var modelScene = scene with { PlaneModel = new(new(movement.X, movement.Y)) };
            var options = Options(modelScene, table.Revision);
            if (_directLayerReplay && ui.TryReplayLayer(table.MapId, options)) continue;
            if (ui.BeginLayer(table.MapId, options))
            {
                _builds++;
                var origin = Origin(map, table.GeometryOrigin);
                var draw = ui.GetWindowDrawList();
                draw.AddRectFilledRounded(new(origin.X, origin.Y, TableWidth, TableHeight), table.Accent, ui.WhiteTextureId, 10, draw.GetClipRect());
            }
            ui.EndLayer();
        }
        var mapScale = MathF.Min(map.Width / _worldWidth, map.Height / _worldHeight) * .9f;
        var viewWidth = board.Width / _zoom;
        var viewHeight = board.Height / _zoom;
        var center = -_pan / _zoom;
        var area = new UiRect(map.X + map.Width * .5f + (center.X - viewWidth * .5f) * mapScale,
            map.Y + map.Height * .5f + (center.Y - viewHeight * .5f) * mapScale,
            viewWidth * mapScale, viewHeight * mapScale);
        var overlay = ui.GetWindowDrawList();
        overlay.PushClipRect(map);
        overlay.AddRectFilled(area, new(0x3067DDFF), ui.WhiteTextureId);
        overlay.AddRect(area, new(0xE0B3E9FF), 1, 1.5f);
        overlay.PopClipRect();
    }

    private void DrawControls(UiImmediateContext ui, UiVector2 work, UiRect board, UiRendererFrameMetrics metrics)
    {
        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(12, 10));
        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(8, 4));
        if (_statusText.Length == 0 || ui.GetTime() >= _nextStatusTime)
        {
            _nextStatusTime = ui.GetTime() + .25;
            _statusText = string.Create(CultureInfo.InvariantCulture,
                $"FPS: {_displayFps:0.0} / {TargetFps}\nZoom: {_zoom * 100:0}%\nScene builds/frame: {_lastBuilds}\nScene builds total: {_builds}\nFrame upload: {(metrics.DynamicUploadBytes + metrics.StagingUploadBytes + metrics.CompiledIndexUploadBytes + metrics.LayerStateUploadBytes) / 1024.0:0.0} KB\nDraw calls: {metrics.DrawCalls}\nLayer draws: {metrics.CompiledLayerDraws}\nFrame CPU: {ui.GetRenderTimeMs() + ui.GetSubmitTimeMs():0.00} ms\nGPU (delayed): {metrics.GpuRenderMicroseconds:0.0} us\n\nWheel: zoom at cursor\nMiddle drag: pan canvas\nCtrl + middle: drag zoom\nRight click: context menu\nLeft drag: move table\n\nIdle policy: {(_fullRebuild ? "continuous" : "on demand")}\nOverview: independent");
            _statusLines = _statusText.Split('\n');
        }
        var statusHeight = ui.CalcTextSize(_statusText).Y;
        var panelHeight = ui.GetFrameHeight() + 10 + ui.CalcTextSize(_introduction).Y
            + 5 * (ui.GetFrameHeight() + 4) + statusHeight + 36;
        ui.SetNextWindowPos(new(work.X + 12, work.Y + 12));
        ui.SetNextWindowSize(new(248, MathF.Ceiling(panelHeight)));
        ui.BeginPanel("ERD / Layer Lab");
        ui.Text(_introduction);
        if (ui.Button(_fullRebuild ? "Update: REBUILD ALL" : "Update: CHANGED ONLY") && _scenario == "interactive") _fullRebuild = !_fullRebuild;
        if (ui.Button(_autoCamera ? "Stop camera tour" : "Start camera tour") && _scenario == "interactive")
        { _autoCamera = !_autoCamera; _tourElapsed = 0; _lastTourCycle = -1; _tourVisits.Clear(); _tourSeed = _selected; _anchoredZoom = false; DuxelApp.RequestFrame(); }
        var speed = _tourSpeed;
        ui.SetNextItemWidth(120);
        if (ui.SliderFloat("Tour speed", ref speed, .15f, 1.2f) && _scenario == "interactive") _tourSpeed = speed;
        if (ui.Button(_animateTable ? "Stop motion" : "Animate one table") && _scenario == "interactive")
        { _animateTable = !_animateTable; if (_animateTable) StartTableMotion(board); else _animatedIndex = -1; DuxelApp.RequestFrame(); }
        if (ui.Button("Fit diagram") && _scenario == "interactive") { _autoCamera = false; _anchoredZoom = false; _targetZoom = _fitZoom; _targetPan = default; }
        DrawStatusLines(ui, statusHeight);
        ui.EndPanel();
        ui.PopStyleVar(2);
        ui.SetCursorScreenPos(new(board.X + 8, work.Y + 18));
        ui.TextColored(new(0xFFE8D8C5), "SCHEMA EXPLORER    /    commerce.public");
    }

    private void DrawStatusLines(UiImmediateContext ui, float height)
    {
        // Subtract two multiline measurements to isolate line advance from ink bounds.
        var shortHeight = ui.CalcTextSize(StatusLineAdvanceShort).Y;
        var longHeight = ui.CalcTextSize(StatusLineAdvanceLong).Y;
        var advance = (longHeight - shortHeight) / StatusLineAdvanceIntervals;
        var start = ui.GetCursorScreenPos();
        var retainStatus = OperatingSystem.IsWindows() && ui.ContentScale == 1 && ui.GetDirectTextEnabled();
        var textColor = ui.GetStyle().Text;
        for (var row = 0; row < _statusLines.Length; row++)
        {
            var line = _statusLines[row];
            if (line.Length == 0)
                continue;
            var position = new UiVector2(start.X, start.Y + row * advance);
            ui.SetCursorScreenPos(position);
            // Build totals, CPU time and GPU time produce new values most often.
            var layerIndex = row switch { 3 => 0, 7 => 1, 8 => 2, _ => -1 };
            if (layerIndex < 0)
            {
                ui.Text(line);
                continue;
            }
            if (!retainStatus)
            {
                ui.TextWithShortCache(line);
                continue;
            }

            ref var layer = ref _statusLayers[layerIndex];
            if (layer.Text != line || layer.Position != position || layer.Color != textColor || layer.LineAdvance != advance)
                layer = new(line, position, textColor, advance, layer.Version + 1);
            if (ui.BeginLayer(StatusLayerIds[layerIndex], new(false, 1, default) { ContentVersion = layer.Version }))
                ui.TextWithShortCache(line);
            ui.EndLayer();
        }
        ui.SetCursorScreenPos(new(start.X, start.Y + height + ui.GetItemSpacing().Y));
    }

    public void SaveMeasurement(double processCpuMilliseconds, double processWallMilliseconds, long processPeakWorkingSetBytes)
    {
        CollectPresentedFrames();
        if (_frames is not { } frames) return;
        if (_recordedWorkloadTimestamps is { } workloadTimestamps)
        {
            using var writer = new StreamWriter(_output + ".workload.csv");
            writer.WriteLine("drawDataFrameId,workloadTimestampTicks");
            for (var i = 0; i < frames.Count; i++)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{_recordedSourceFrameIds![i]},{workloadTimestamps[i]}"));
        }
        var warmupMethod = _workloadClock is null
            ? "first-accepted-presentation-v1" : "first-accepted-presentation-source-epoch-v2";
        var workloadMethod = _workloadClock is null ? "app-wall-total-v1" : "post-warmup-source-v1";
        var rawPath = _output + ".frames.csv";
        using (var writer = new StreamWriter(rawPath))
        {
            writer.WriteLine("frameSeconds");
            foreach (var seconds in frames.FrameSeconds) writer.WriteLine(seconds.ToString("R", CultureInfo.InvariantCulture));
        }
        using (var writer = new StreamWriter(_output + ".presentations.csv"))
        {
            writer.WriteLine("presentationSequence,drawDataFrameId");
            for (var i = 0; i < frames.Count; i++)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{_firstMeasuredPresentationSequence + (ulong)i},{_recordedSourceFrameIds![i]}"));
        }
        var performance = "null";
        if (frames.Count > 0)
        {
            var stats = frames.Calculate();
            performance = string.Create(CultureInfo.InvariantCulture,
                $"{{\"samples\":{stats.Samples},\"averageFps\":{stats.AverageFps},\"medianMs\":{stats.MedianFrameMs},\"p95Ms\":{stats.P95FrameMs},\"p99Ms\":{stats.P99FrameMs},\"low1PctFps\":{stats.Low1PctFps},\"sceneBuildsPerFrame\":{(double)_buildSum / stats.Samples},\"sceneAllocatedBytesPerFrame\":{(double)_allocationSum / stats.Samples},\"rendererAllocatedBytesPerFrame\":{(_rendererAllocationSamples > 0 ? ((double)_rendererAllocationSum / _rendererAllocationSamples).ToString("R", CultureInfo.InvariantCulture) : "null")},\"frameUploadBytes\":{(double)_uploadSum / stats.Samples},\"layerStateUploadBytes\":{(double)_layerStateUploadSum / stats.Samples},\"compiledIndexUploadBytes\":{(double)_compiledIndexUploadSum / stats.Samples},\"compiledLayerDraws\":{(double)_compiledDrawSum / stats.Samples},\"sharedBasisLayerDraws\":{(double)_sharedBasisDrawSum / stats.Samples},\"sharedCameraLayerDraws\":{(double)_sharedCameraDrawSum / stats.Samples},\"compiledLayerCommands\":{(double)_compiledCommandSum / stats.Samples},\"compiledDescriptorBuilds\":{(double)_descriptorBuildSum / stats.Samples},\"compiledCommandVisits\":{(double)_commandVisitSum / stats.Samples},\"layerBoundsCommandVisits\":{(double)_boundsVisitSum / stats.Samples},\"drawCalls\":{(double)_drawSum / stats.Samples},\"gpuMicroseconds\":{(_gpuSamples > 0 ? (_gpuSum / _gpuSamples).ToString("R", CultureInfo.InvariantCulture) : "null")}}}");
        }
        var tourVisits = "[" + string.Join(',', _tourVisits) + "]";
        var selection = string.Create(CultureInfo.InvariantCulture,
            $"{{\"entity\":{_selected},\"relationships\":{_tableRelations[_selected].Length},\"lineWidth\":{SelectedRelationThickness},\"tableDepth\":{.4 - .2 * (double)_tables[_selected].DepthRank / _frontSequence},\"lineDepth\":{.4 - .2 * (_tables[_selected].DepthRank - .5d) / _frontSequence}}}");
        var drag = string.Create(CultureInfo.InvariantCulture,
            $"{{\"entity\":{_measuredDragEntity},\"startFrame\":{_dragStartFrame},\"twoSecondsFrame\":{_dragTwoSecondsFrame},\"lastFrame\":{_dragLastFrame},\"startTime\":{_dragStartTime},\"lastTime\":{_dragLastTime},\"motionFrames\":{_dragMotionFrames},\"worldDistance\":{_dragWorldDistance}}}");
        File.WriteAllText(_output, string.Create(CultureInfo.InvariantCulture,
            $"{{\"workloadTimingBasis\":\"{workloadMethod}\",\"workloadStartTicks\":{_workloadClock?.StartTimestampTicks ?? 0},\"workloadDeadlineTicks\":{_workloadClock?.DeadlineTimestampTicks ?? 0},\"workloadDurationSeconds\":{_workloadClock?.DurationSeconds ?? 0},\"workloadInitialZoom\":{_workloadInitialZoom},\"workloadInitialPanX\":{_workloadInitialPan.X},\"workloadInitialPanY\":{_workloadInitialPan.Y},\"firstMeasuredWorkloadTimestampTicks\":{_firstMeasuredWorkloadTimestampTicks},\"lastMeasuredWorkloadTimestampTicks\":{_lastMeasuredWorkloadTimestampTicks},\"lastMeasuredPresentationTimestampTicks\":{_lastMeasuredPresentationTimestampTicks},\"axisAlignedLayerCulling\":{(_axisAlignedLayerCulling ? "true" : "false")},\"compiledIndexLimit\":{_compiledIndexLimit},\"compiledIndexCachePeakBytes\":{_compiledIndexCachePeakBytes},\"cpuTimingCollectedFrames\":{_cpuTimingCollectedFrames},\"gpuTimingSamples\":{_gpuSamples},\"eventDrivenPlatformLoop\":{(_eventDrivenPlatformLoop ? "true" : "false")},\"processCpuMilliseconds\":{processCpuMilliseconds},\"processWallMilliseconds\":{processWallMilliseconds},\"averageBusyCpuCores\":{processCpuMilliseconds / processWallMilliseconds},\"processPeakWorkingSetBytes\":{processPeakWorkingSetBytes},\"renderPipeline\":{(_renderPipeline ? "true" : "false")},\"fpsTarget\":{TargetFps},\"fpsTimingBasis\":\"accepted-presentation\",\"fpsSamplingMethod\":\"presentation-history-v1\",\"fpsWarmupMethod\":\"{warmupMethod}\",\"warmupPresentationStartTicks\":{_warmupPresentationStartTicks},\"stopwatchFrequency\":{Stopwatch.Frequency},\"warmupPresentationDeadlineTicks\":{_warmupPresentationDeadlineTicks},\"firstMeasuredPresentationTimestampTicks\":{_firstMeasuredPresentationTimestampTicks},\"presentationHistoryOverwrites\":{_presentationReader?.OverwrittenSamples ?? 0},\"directLayerReplay\":{(_directLayerReplay ? "true" : "false")},\"uiFrames\":{_renderedFrames},\"framework\":\"{RuntimeInformation.FrameworkDescription}\",\"tables\":{_tableCount},\"relationships\":{_relations.Length},\"view\":\"{(_denseView ? "dense" : "explore")}\",\"scenario\":\"{_scenario}\",\"mode\":\"{(_fullRebuild ? "full" : "resident")}\",\"wallSeconds\":{_wall.Elapsed.TotalSeconds},\"renderedFrames\":{_lastPresentationSequence},\"warmupLastFrame\":{_warmupPresentationSequence},\"framesAfterWarmup\":{(_warmupFrame > 0 ? _lastPresentationSequence - _warmupPresentationSequence : 0)},\"sceneBuilds\":{_builds},\"tourSpeed\":{_tourSpeed},\"minimumZoom\":{_minimumZoom},\"maximumZoom\":{_maximumZoom},\"maximumMotionSpeed\":{_maximumMotionSpeed},\"animatedEntity\":{_animatedIndex},\"tourVisits\":{tourVisits},\"selection\":{selection},\"drag\":{drag},\"performance\":{performance}}}"));
    }
}
