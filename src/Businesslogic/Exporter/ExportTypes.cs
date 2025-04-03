namespace CrowsNestMqtt.Businesslogic.Exporter;

/// <summary>
/// Defines the types of available message exporters.
/// </summary>
public enum ExportTypes
{
    /// <summary>
    /// Represents an exporter that outputs in JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// Represents an exporter that outputs in plain text format.
    /// </summary>
    Text
}