using System.Runtime.InteropServices;
using CrowsNestMqtt.UI.Services;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class LibVlcPathResolverTests
{
    [Theory]
    [InlineData(Architecture.X64, "win-x64")]
    [InlineData(Architecture.X86, "win-x86")]
    [InlineData(Architecture.Arm64, "win-arm64")]
    public void GetWindowsLibraryDirectory_ReturnsPackageArchitectureDirectory(
        Architecture architecture,
        string expectedDirectory)
    {
        var baseDirectory = Path.Combine("app", "output");

        var result = LibVlcPathResolver.GetWindowsLibraryDirectory(baseDirectory, architecture);

        Assert.Equal(Path.Combine(baseDirectory, "libvlc", expectedDirectory), result);
    }

    [Fact]
    public void GetWindowsLibraryDirectory_WithUnsupportedArchitecture_Throws()
    {
        Assert.Throws<PlatformNotSupportedException>(() =>
            LibVlcPathResolver.GetWindowsLibraryDirectory("app", Architecture.Wasm));
    }
}
