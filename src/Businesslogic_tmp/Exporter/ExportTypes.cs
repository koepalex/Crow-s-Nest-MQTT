namespace CrowsNestMqtt.BusinessLogic.Exporter;

/// <summary>
/// Defines the types of available message exporters.
/// </summary>
public enum ExportTypes
{
    /// <summary>
    /// Represents an exporter that outputs in JSON format.
    /// </summary>
    json,

    /// <summary>
    /// Represents an exporter that outputs in plain text format.
    /// </summary>
    txt
}