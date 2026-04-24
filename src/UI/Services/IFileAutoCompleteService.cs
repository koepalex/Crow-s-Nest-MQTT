namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Suggestion returned by the file autocomplete service.
/// </summary>
public record FileAutoCompleteSuggestion(
    string Path,
    string DisplayName,
    bool IsDirectory,
    long? SizeBytes,
    string? Extension);

/// <summary>
/// Service for providing file path autocomplete suggestions for the @ syntax.
/// </summary>
public interface IFileAutoCompleteService
{
    /// <summary>
    /// Base directory used to resolve relative @paths and to scan for completion suggestions.
    /// </summary>
    string BasePath { get; }

    /// <summary>
    /// Gets file path suggestions matching the partial path.
    /// Absolute (rooted) partial paths return no suggestions — the command
    /// line itself is already complete and should not be auto-rewritten.
    /// </summary>
    /// <param name="partialPath">The partial file path to match against.</param>
    /// <param name="maxResults">Maximum number of suggestions to return.</param>
    /// <returns>List of file path suggestions. Each suggestion's <see cref="FileAutoCompleteSuggestion.Path"/> is an absolute path.</returns>
    List<FileAutoCompleteSuggestion> GetSuggestions(string partialPath, int maxResults = 20);
}
