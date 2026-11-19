using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    // AT-SPI transfers the array, each relation, and each queried target to the caller.
    private static bool HasNativeLabel(nint accessible, nint expectedLabel)
    {
        nint error = 0;
        var relations = atspi_accessible_get_relation_set(accessible, &error);
        try
        {
            Error(ref error);
            if (relations == 0 || g_array_get_element_size(relations) != sizeof(nint))
                throw new InvalidDataException("Missing or incompatible native relation array.");
            // The public GLib GArray layout is shared with the bounded-range reader.
            var array = *(NativeRangeArray*)relations;
            var labels = 0;
            var matches = 0;
            for (var i = 0; i < array.Length; i++)
            {
                var relation = ((nint*)array.Data)[i];
                if (atspi_relation_get_relation_type(relation) != 2)
                    continue;
                labels++;
                if (atspi_relation_get_n_targets(relation) != 1)
                    continue;
                using var target = new NativeObject(atspi_relation_get_target(relation, 0));
                if (target.Value == expectedLabel)
                    matches++;
            }
            return labels == 1 && matches == 1;
        }
        finally
        {
            if (relations != 0)
            {
                if (g_array_get_element_size(relations) == sizeof(nint))
                {
                    var array = *(NativeRangeArray*)relations;
                    for (var i = 0; i < array.Length; i++)
                        g_object_unref(((nint*)array.Data)[i]);
                }
                g_array_unref(relations);
            }
        }
    }

    [LibraryImport(Library)] private static partial nint atspi_accessible_get_relation_set(nint accessible, nint* error);
    [LibraryImport(Library)] private static partial int atspi_relation_get_relation_type(nint relation);
    [LibraryImport(Library)] private static partial int atspi_relation_get_n_targets(nint relation);
    [LibraryImport(Library)] private static partial nint atspi_relation_get_target(nint relation, int index);
}
