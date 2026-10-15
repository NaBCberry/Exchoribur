using System.Reflection;
using Exchoribur.Application.Devices;
using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.App.Tests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Core_does_not_depend_on_external_adapters_or_application_code()
        => AssertNoReferences(typeof(Timeline).Assembly,
            "Exchoribur.Application", "Exchoribur.Infrastructure", "Exchoribur.App", "Avalonia", "LibVLCSharp", "System.IO.Ports");

    [Fact]
    public void Application_does_not_depend_on_desktop_or_infrastructure()
        => AssertNoReferences(typeof(DeviceService).Assembly,
            "Exchoribur.Infrastructure", "Exchoribur.App", "Avalonia", "LibVLCSharp", "System.IO.Ports");

    [Fact]
    public void Infrastructure_does_not_depend_on_the_desktop_framework()
        => AssertNoReferences(typeof(ProjectStore).Assembly, "Exchoribur.App", "Avalonia");

    private static void AssertNoReferences(Assembly assembly, params string[] forbidden)
    {
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(forbidden, prefix => reference.Name == prefix
                || reference.Name!.StartsWith(prefix + ".", StringComparison.Ordinal));
        }
    }
}
