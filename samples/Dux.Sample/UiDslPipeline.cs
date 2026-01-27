using System;
using Dux.Core.Dsl;

internal static partial class UiDslPipeline
{
    public static Action<IUiDslEmitter> CreateRenderer(string uiName, string uiPath)
        => CreateRendererCore(uiName, uiPath);

    private static partial Action<IUiDslEmitter> CreateRendererCore(string uiName, string uiPath);
}
