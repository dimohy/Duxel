using System;
using Dux.Core.Dsl;
using Dux.Generated.Ui;

internal static partial class UiDslPipeline
{
    private static partial Action<IUiDslEmitter> CreateRendererCore(string uiName, string uiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uiName);
        _ = uiPath;

        return UiDslGeneratedRegistry.GetRenderer(uiName);
    }
}
