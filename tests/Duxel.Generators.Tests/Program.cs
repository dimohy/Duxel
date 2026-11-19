using System.Reflection;
using System.Runtime.ExceptionServices;

var mode = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(attribute => attribute.Key == "DuxelRoslynRuntimeMode").Value;
if (mode == "Sdk")
{
    var context = new SdkCompilerContext(Assembly.GetExecutingAssembly().Location);
    try
    {
        var assembly = context.LoadFromAssemblyPath(Assembly.GetExecutingAssembly().Location);
        var runner = assembly.GetType("GeneratorTestRunner", throwOnError: true)!
            .GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)!;
        runner.Invoke(null, [args]);
    }
    catch (TargetInvocationException error) when (error.InnerException is not null)
    {
        ExceptionDispatchInfo.Capture(error.InnerException).Throw();
    }
    finally { context.Unload(); }
    return;
}
else if (mode != "Package")
    throw new InvalidOperationException("Unknown explicit compiler reference mode: " + mode);

GeneratorTestRunner.Run(args);
