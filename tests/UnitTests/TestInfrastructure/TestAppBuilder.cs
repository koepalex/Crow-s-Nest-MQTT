using Avalonia;
using Avalonia.Headless;
using CrowsNestMqtt.UnitTests.TestInfrastructure;

// Wires Avalonia.Headless.XUnit's runner to the CrowsNestMqtt.UI.App used at runtime.
// The runner constructs the AppBuilder once per assembly (per AvaloniaTestIsolationLevel below)
// and marshals every [AvaloniaFact] / [AvaloniaTheory] test onto its owned Avalonia dispatcher,
// so Compositor thread-affinity checks (Avalonia 12.1+) always pass regardless of which xUnit
// pool thread the test happens to be scheduled on.
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// PerAssembly reuses a single Application/Dispatcher across the whole test run — matches the
// prior custom AvaloniaFixture's single-instance semantics and avoids the per-test setup cost.
// If a test surfaces shared-state contamination, switch to AvaloniaTestIsolationLevel.PerTest.
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace CrowsNestMqtt.UnitTests.TestInfrastructure;

/// <summary>
/// Provides the <see cref="AppBuilder"/> that Avalonia.Headless.XUnit uses to construct the
/// test-application. Referenced via <c>[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]</c>
/// above.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "AvaloniaTestApplication attribute requires a public builder type — the runner reflects on it from Avalonia.Headless.XUnit.")]
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<CrowsNestMqtt.UI.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
