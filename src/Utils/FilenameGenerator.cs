namespace CrowsNestMqtt.Utils;

/// <summary>
/// Utility for generating safe, cross-platform filenames for MQTT message exports.
/// T018: Created as part of export all messages feature.
/// </summary>
public static class FilenameGenerator
{
    // Characters that are invalid in filenames on various operating systems
    private static readonly char[] InvalidFilenameChars = new char[]
    {
        ':', '?', '*', '<', '>', '/', '\\', '|', '"', '\0',
        '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
        '\u0008', '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u000E',
        '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015',
        '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C',
        '\u001D', '\u001E', '\u001F'
    };

    /// <summary>
    /// Generates a safe filename for bulk export using pattern: topic-name_timestamp.ext
    /// </summary>
    /// <param name="topicName">The MQTT topic name (may contain wildcards or hierarchy separators).</param>
    /// <param name="timestamp">The timestamp for the export operation.</param>
    /// <param name="extension">The file extension (e.g., "json", "txt").</param>
    /// <returns>A safe filename suitable for all platforms.</returns>
    /// <exception cref="ArgumentException">Thrown when topicName or extension is null or empty.</exception>
    /// <example>
    /// GenerateExportAllFilename("sensors/temperature", DateTime.Now, "json")
    /// → "sensors_temperature_20260121_143045.json"
    /// </example>
    public static string GenerateExportAllFilename(string topicName, DateTime timestamp, string extension)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be null or empty.", nameof(topicName));

        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));

        // Sanitize topic name
        string sanitizedTopic = SanitizeTopicName(topicName);

        // Format timestamp (ISO 8601-like, filesystem-safe: YYYYMMdd_HHmmss)
        string timestampStr = timestamp.ToString("yyyyMMdd_HHmmss");

        // Ensure extension doesn't have leading dot
        string ext = extension.TrimStart('.');

        return $"{sanitizedTopic}_{timestampStr}.{ext}";
    }

    /// <summary>
    /// Sanitizes an MQTT topic name to be filesystem-safe.
    /// Replaces hierarchy separators (/) and wildcards (+, #) with underscores.
    /// </summary>
    /// <param name="topicName">The topic name to sanitize.</param>
    /// <returns>A sanitized topic name safe for filenames.</returns>
    /// <example>
    /// SanitizeTopicName("sensors/temperature") → "sensors_temperature"
    /// SanitizeTopicName("sensor/+") → "sensor_+"
    /// SanitizeTopicName("sensor/#") → "sensor_#"
    /// </example>
    public static string SanitizeTopicName(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return "unknown";

        // Replace invalid characters with underscores
        var sanitized = topicName;
        foreach (char invalidChar in InvalidFilenameChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // MQTT hierarchy separator '/' is valid in topic but not in filenames
        sanitized = sanitized.Replace('/', '_');

        // Truncate if too long (max filename length is typically 255 on most systems)
        // Leave room for timestamp and extension (~30 chars)
        const int maxTopicLength = 200;
        if (sanitized.Length > maxTopicLength)
        {
            sanitized = sanitized.Substring(0, maxTopicLength);
        }

        // Handle edge case: empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "sanitized_topic";
        }

        return sanitized;
    }

    /// <summary>
    /// Generates a unique filename by appending a counter if the file already exists.
    /// </summary>
    /// <param name="basePath">The base path including filename and extension.</param>
    /// <returns>A unique file path that doesn't exist.</returns>
    /// <example>
    /// GetUniqueFilename("/exports/topic_20260121_143045.json")
    /// If file exists → "/exports/topic_20260121_143045_1.json"
    /// If that exists → "/exports/topic_20260121_143045_2.json"
    /// </example>
    public static string GetUniqueFilename(string basePath)
    {
        if (!File.Exists(basePath))
            return basePath;

        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var filenameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        int counter = 1;
        string newPath;

        do
        {
            newPath = Path.Combine(directory, $"{filenameWithoutExt}_{counter}{extension}");
            counter++;
        }
        while (File.Exists(newPath) && counter < 1000); // Safety limit

        return newPath;
    }

    /// <summary>
    /// Validates that a path is safe and writable.
    /// </summary>
    /// <param name="path">The file or directory path to validate.</param>
    /// <returns>True if the path is valid and potentially writable.</returns>
    public static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Try to get full path - this validates path format
            _ = Path.GetFullPath(path);

            // Check if parent directory exists or can be created
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                return Directory.Exists(directory) || CanCreateDirectory(directory);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanCreateDirectory(string path)
    {
        try
        {
            // Check if we have permissions to create directory
            var parent = Path.GetDirectoryName(path);
            return parent == null || Directory.Exists(parent);
        }
        catch
        {
            return false;
        }
    }
}
