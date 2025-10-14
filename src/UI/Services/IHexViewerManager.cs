namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages hex viewer display and binary payload handling.
/// </summary>
public interface IHexViewerManager
{
    /// <summary>
    /// Gets whether the hex viewer is currently visible.
    /// </summary>
    bool IsHexViewerVisible { get; }

    /// <summary>
    /// Gets the current hex payload bytes being displayed.
    /// </summary>
    byte[]? HexPayloadBytes { get; }

    /// <summary>
    /// Determines if the given content type represents binary data.
    /// </summary>
    /// <param name="contentType">The MQTT message content type.</param>
    /// <returns>True if binary, false otherwise.</returns>
    bool IsBinaryContentType(string? contentType);

    /// <summary>
    /// Attempts to load hex view for the given payload bytes.
    /// </summary>
    /// <param name="payloadBytes">The binary payload to display.</param>
    /// <param name="statusMessage">Output status message for user feedback.</param>
    /// <returns>True if successfully loaded, false otherwise.</returns>
    bool TryLoadHexView(byte[] payloadBytes, out string statusMessage);

    /// <summary>
    /// Clears the hex view display.
    /// </summary>
    void ClearHexView();

    /// <summary>
    /// Event raised when hex viewer visibility changes.
    /// </summary>
    event EventHandler<bool>? HexViewerVisibilityChanged;

    /// <summary>
    /// Event raised when hex payload bytes change.
    /// </summary>
    event EventHandler<byte[]?>? HexPayloadChanged;
}
