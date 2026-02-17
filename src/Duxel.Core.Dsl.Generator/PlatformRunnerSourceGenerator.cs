using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Duxel.Core.Dsl.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class PlatformRunnerSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var shouldGenerate = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var appType = compilation.GetTypeByMetadataName("Duxel.App.DuxelApp");
            var windowsAppType = compilation.GetTypeByMetadataName("Duxel.Windows.App.DuxelWindowsApp");
            return appType is not null && windowsAppType is not null;
        });

        context.RegisterSourceOutput(shouldGenerate, static (sourceProductionContext, enabled) =>
        {
            if (!enabled)
            {
                return;
            }

            const string source = """
using System.Runtime.CompilerServices;

namespace Duxel.App;

internal static class GeneratedPlatformRunnerRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        DuxelApp.RegisterRunner(global::Duxel.Windows.App.DuxelWindowsApp.Run);
    }
}
""";

            sourceProductionContext.AddSource(
                "GeneratedPlatformRunnerBootstrap.g.cs",
                SourceText.From(source, Encoding.UTF8)
            );
        });
    }
}
