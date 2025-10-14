using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages image payload loading, display, and operations.
/// </summary>
public class ImageViewerManager : IImageViewerManager, IDisposable
{
    private bool _isImageViewerVisible;
    private Bitmap? _imagePayload;
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsImageViewerVisible
    {
        get => _isImageViewerVisible;
        private set
        {
            if (_isImageViewerVisible != value)
            {
                _isImageViewerVisible = value;
                ImageViewerVisibilityChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc/>
    public Bitmap? ImagePayload
    {
        get => _imagePayload;
        private set
        {
            if (_imagePayload != value)
            {
                _imagePayload?.Dispose();
                _imagePayload = value;
                ImagePayloadChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<bool>? ImageViewerVisibilityChanged;

    /// <inheritdoc/>
    public event EventHandler<Bitmap?>? ImagePayloadChanged;

    /// <inheritdoc/>
    public bool TryLoadImage(byte[] payloadBytes, out string statusMessage)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
        {
            statusMessage = "No image data to display";
            ClearImage();
            return false;
        }

        try
        {
            using var ms = new MemoryStream(payloadBytes);
            var bitmap = new Bitmap(ms);

            ImagePayload = bitmap;
            IsImageViewerVisible = true;
            statusMessage = $"Image loaded ({bitmap.PixelSize.Width}x{bitmap.PixelSize.Height})";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Failed to decode image: {ex.Message}";
            ClearImage();
            return false;
        }
    }

    /// <inheritdoc/>
    public void ClearImage()
    {
        IsImageViewerVisible = false;
        ImagePayload = null;
    }

    /// <summary>
    /// Disposes resources used by the image viewer manager.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _imagePayload?.Dispose();
            _imagePayload = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
