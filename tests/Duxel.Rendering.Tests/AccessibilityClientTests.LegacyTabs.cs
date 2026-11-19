using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckLegacyTabSelection(nint automation, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var owner = found["Legacy event tabs"];
        // Panels legitimately share their header's name; query inside the actual TabList.
        nint condition = 0, headers = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
        owned.Add(condition);
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(owner, 6))(owner, 2, condition, &headers));
        owned.Add(headers);
        int headerCount = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(headers, 3))(headers, &headerCount));
        var headerNodes = new Dictionary<string, nint>(StringComparer.Ordinal);
        for (var i = 0; i < headerCount; i++)
        {
            nint header = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(headers, 4))(headers, i, &header));
            owned.Add(header);
            headerNodes.Add(Text(header, 30005), header);
        }
        var alpha = headerNodes["Alpha"];
        var beta = headerNodes["Beta"];
        var selection = Pattern(owner, 10001);
        var alphaPattern = Pattern(alpha, 10010);
        var betaPattern = Pattern(beta, 10010);
        owned.Add(selection); owned.Add(alphaPattern); owned.Add(betaPattern);
        string[] SelectedNames()
        {
            nint array = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(selection, 3))(selection, &array));
            if (array == 0) throw new InvalidOperationException("Selection array is null.");
            try
            {
                int count = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(array, 3))(array, &count));
                var names = new string[count];
                for (var i = 0; i < count; i++)
                {
                    nint element = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(array, 4))(array, i, &element));
                    try { names[i] = Text(element, 30005); }
                    finally { Marshal.Release(element); }
                }
                return names;
            }
            finally { Marshal.Release(array); }
        }
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        check(Boolean(owner, 30061) && SelectedNames().SequenceEqual(new[] { "Alpha" }),
            "external legacy TabList exposes required single selection through the native Selection pattern");
        var alphaPanel = CheckControlledPanel(alpha, owned, check, focusable : false);
        check(NoControlledPanel(beta, owned), "external inactive legacy tab has no fabricated panel");
        ReadRelationshipChanges(automation, root, owned, [30104], relationships =>
        ReadPropertyChanges(automation, root, 4, owned, [30079], properties =>
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(betaPattern, 3))(betaPattern));
            var changed = new[] { properties.Wait(), properties.Wait() }.ToHashSet();
            check(changed.SetEquals(new[]
                {
                    new TooltipNativeProperty(30079, Text(alpha, 30011), false),
                    new TooltipNativeProperty(30079, Text(beta, 30011), true)
                }), "external legacy selection emits both committed selected-property changes");
            check(Boolean(beta, 30079) && !Boolean(alpha, 30079) && SelectedNames().SequenceEqual(new[] { "Beta" }),
                "external legacy selection notifications reflect the actual committed native selection state");
            var betaPanel = CheckControlledPanel(beta, owned, check, focusable : false);
            var relationshipEvents = new[] { WaitRelationship(relationships, owned), WaitRelationship(relationships, owned) };
            check(relationshipEvents.Select(x => (x.Source, x.TargetId)).ToHashSet().SetEquals(new[]
                {
                    (Text(alpha, 30011), (string?)null),
                    (Text(beta, 30011), (string?)Text(betaPanel, 30011))
                }), "external legacy tabs emit controller removal and new live-panel relationship payloads");
        }));
        WaitForSlider(() => TooltipElementUnavailable(alphaPanel));
        check(NoControlledPanel(alpha, owned), "external legacy switching invalidates the old panel and clears its controller relationship");
        check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(betaPattern, 5))(betaPattern) == UiaNative.InvalidOperation,
            "external legacy required selection rejects RemoveFromSelection");
        Invoke("Tab action");
        Invoke("Remove selected");
        WaitForSlider(() => TooltipElementUnavailable(beta) && SelectedNames().Length == 0);
        check(SelectedNames().Length == 0, "external removal does not fabricate a replacement legacy selection");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(alphaPattern, 3))(alphaPattern));
        WaitForSlider(() => SelectedNames().SequenceEqual(new[] { "Alpha" }) && Boolean(alpha, 30079));
        check(SelectedNames().SequenceEqual(new[] { "Alpha" }) && Boolean(alpha, 30079),
            "external legacy selection recovers through an actual surviving header and commits its body");
        Invoke("Finish");
    }
}
