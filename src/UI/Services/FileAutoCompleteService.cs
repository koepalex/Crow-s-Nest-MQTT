using Serilog;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Provides file path autocomplete suggestions by scanning the filesystem.
/// Used for the @ file reference syntax in the publish command and dialog.
/// </summary>
public class FileAutoCompleteService : IFileAutoCompleteService
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "node_modules", "bin", "obj", "__pycache__", ".svn"
    };

    private readonly string _basePath;

    /// <summary>
    /// Creates a new FileAutoCompleteService.
    /// </summary>
    /// <param name="basePath">Base directory for relative path resolution. Defaults to current directory.</param>
    public FileAutoCompleteService(string? basePath = null)
    {
        _basePath = basePath ?? Directory.GetCurrentDirectory();
    }

    /// <inheritdoc />
    public List<FileAutoCompleteSuggestion> GetSuggestions(string partialPath, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(partialPath))
            return GetRootSuggestions(maxResults);

        try
        {
            var resolvedPath = ResolvePath(partialPath);
            var directory = Path.GetDirectoryName(resolvedPath) ?? _basePath;
            var searchPattern = Path.GetFileName(resolvedPath);

            if (!Directory.Exists(directory))
                return new List<FileAutoCompleteSuggestion>();

            if (string.IsNullOrEmpty(searchPattern) || Directory.Exists(resolvedPath))
            {
                // User typed a complete directory path — list its contents
                var targetDir = Directory.Exists(resolvedPath) ? resolvedPath : directory;
                return GetDirectoryContents(targetDir, maxResults);
            }

            return GetMatchingSuggestions(directory, searchPattern, maxResults);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error getting file autocomplete suggestions for '{PartialPath}'", partialPath);
            return new List<FileAutoCompleteSuggestion>();
        }
    }

    private string ResolvePath(string partialPath)
    {
        if (Path.IsPathRooted(partialPath))
            return partialPath;
        return Path.Combine(_basePath, partialPath);
    }

    private List<FileAutoCompleteSuggestion> GetRootSuggestions(int maxResults)
    {
        return GetDirectoryContents(_basePath, maxResults);
    }

    private List<FileAutoCompleteSuggestion> GetDirectoryContents(string directory, int maxResults)
    {
        var suggestions = new List<FileAutoCompleteSuggestion>();

        try
        {
            // Directories first, then files
            foreach (var dir in Directory.EnumerateDirectories(directory)
                .Where(d => !ExcludedDirectories.Contains(Path.GetFileName(d)))
                .Take(maxResults))
            {
                var dirInfo = new DirectoryInfo(dir);
                suggestions.Add(new FileAutoCompleteSuggestion(
                    GetRelativePath(dir),
                    dirInfo.Name + Path.DirectorySeparatorChar,
                    IsDirectory: true,
                    SizeBytes: null,
                    Extension: null));
            }

            foreach (var file in Directory.EnumerateFiles(directory)
                .Take(maxResults - suggestions.Count))
            {
                var fileInfo = new FileInfo(file);
                suggestions.Add(new FileAutoCompleteSuggestion(
                    GetRelativePath(file),
                    fileInfo.Name,
                    IsDirectory: false,
                    SizeBytes: fileInfo.Length,
                    Extension: fileInfo.Extension));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }

        return suggestions.Take(maxResults).ToList();
    }

    private List<FileAutoCompleteSuggestion> GetMatchingSuggestions(string directory, string searchPattern, int maxResults)
    {
        var suggestions = new List<FileAutoCompleteSuggestion>();

        try
        {
            // Match directories
            foreach (var dir in Directory.EnumerateDirectories(directory)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !ExcludedDirectories.Contains(name) &&
                           name.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase);
                })
                .Take(maxResults))
            {
                var dirInfo = new DirectoryInfo(dir);
                suggestions.Add(new FileAutoCompleteSuggestion(
                    GetRelativePath(dir),
                    dirInfo.Name + Path.DirectorySeparatorChar,
                    IsDirectory: true,
                    SizeBytes: null,
                    Extension: null));
            }

            // Match files
            foreach (var file in Directory.EnumerateFiles(directory)
                .Where(f => Path.GetFileName(f).StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults - suggestions.Count))
            {
                var fileInfo = new FileInfo(file);
                suggestions.Add(new FileAutoCompleteSuggestion(
                    GetRelativePath(file),
                    fileInfo.Name,
                    IsDirectory: false,
                    SizeBytes: fileInfo.Length,
                    Extension: fileInfo.Extension));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }

        return suggestions.Take(maxResults).ToList();
    }

    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_basePath, fullPath);
    }
}
