using Avalonia.Media.Imaging;
using System;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages image payload loading, display, and operations.
/// </summary>
public interface IImageViewerManager
{
    /// <summary>
    /// Gets whether the image viewer is currently visible.
    /// </summary>
    bool IsImageViewerVisible { get; }

    /// <summary>
    /// Gets the current image payload being displayed.
    /// </summary>
    Bitmap? ImagePayload { get; }

    /// <summary>
    /// Attempts to load an image from the given payload bytes.
    /// </summary>
    /// <param name="payloadBytes">The binary image payload.</param>
    /// <param name="statusMessage">Output status message for user feedback.</param>
    /// <returns>True if successfully loaded, false otherwise.</returns>
    bool TryLoadImage(byte[] payloadBytes, out string statusMessage);

    /// <summary>
    /// Clears the image viewer display.
    /// </summary>
    void ClearImage();

    /// <summary>
    /// Event raised when image viewer visibility changes.
    /// </summary>
    event EventHandler<bool>? ImageViewerVisibilityChanged;

    /// <summary>
    /// Event raised when image payload changes.
    /// </summary>
    event EventHandler<Bitmap?>? ImagePayloadChanged;
}
