using Xunit;

namespace CrowsNestMqtt.UnitTests.UI;

/// <summary>
/// Custom [AvaloniaFact] attribute for xUnit v3 compatibility.
/// Replaces Avalonia.Headless.XUnit's AvaloniaFact which is only available for xUnit v2
/// in Avalonia 11.x. Tests using this attribute run on the Avalonia UI thread via the
/// shared AvaloniaFixture/AvaloniaCollection infrastructure.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AvaloniaFactAttribute : FactAttribute
{
    public AvaloniaFactAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
    }
}
