using System.Runtime.CompilerServices;
using Quartz.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class TestAssembly
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Quartz's logging provider is process-global. WebApplicationFactory
        // creates and disposes several hosts in this test assembly, so a
        // provider tied to the first host's LoggerFactory would be stale.
        LogProvider.IsDisabled = true;
    }
}
