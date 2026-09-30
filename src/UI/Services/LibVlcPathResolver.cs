using System.Runtime.InteropServices;

namespace CrowsNestMqtt.UI.Services;

internal static class LibVlcPathResolver
{
    internal static string GetWindowsLibraryDirectory(
        string baseDirectory,
        Architecture architecture)
    {
        var architectureDirectory = architecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            _ => throw new PlatformNotSupportedException(
                $"LibVLC is not packaged for Windows architecture '{architecture}'.")
        };

        return Path.Combine(baseDirectory, "libvlc", architectureDirectory);
    }
}
