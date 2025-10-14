using System;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages hex viewer display and binary payload handling.
/// </summary>
public class HexViewerManager : IHexViewerManager
{
    private bool _isHexViewerVisible;
    private byte[]? _hexPayloadBytes;

    /// <inheritdoc/>
    public bool IsHexViewerVisible
    {
        get => _isHexViewerVisible;
        private set
        {
            if (_isHexViewerVisible != value)
            {
                _isHexViewerVisible = value;
                HexViewerVisibilityChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc/>
    public byte[]? HexPayloadBytes
    {
        get => _hexPayloadBytes;
        private set
        {
            if (_hexPayloadBytes != value)
            {
                _hexPayloadBytes = value;
                HexPayloadChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<bool>? HexViewerVisibilityChanged;

    /// <inheritdoc/>
    public event EventHandler<byte[]?>? HexPayloadChanged;

    /// <inheritdoc/>
    public bool IsBinaryContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var ct = contentType.Trim().ToLowerInvariant();

        // Known binary content types
        var binaryContentTypes = new[]
        {
            "application/octet-stream",
            "application/pdf",
            "application/zip",
            "application/gzip",
            "application/x-gzip",
            "application/x-tar",
            "application/x-7z-compressed",
            "application/x-rar-compressed",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/x-msdownload",
            "application/x-executable",
            "application/x-sharedlib",
            "application/java-archive",
            "application/x-java-archive",
            "application/protobuf",
            "application/x-protobuf",
            "application/grpc",
            "application/cbor",
            "application/msgpack",
            "application/x-msgpack",
            "application/avro",
            "application/x-avro"
        };

        foreach (var binType in binaryContentTypes)
        {
            if (ct.Equals(binType, StringComparison.OrdinalIgnoreCase) ||
                ct.StartsWith(binType + ";", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for image/video types (also binary)
        if (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            ct.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            ct.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool TryLoadHexView(byte[] payloadBytes, out string statusMessage)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
        {
            statusMessage = "No payload data to display";
            ClearHexView();
            return false;
        }

        try
        {
            HexPayloadBytes = payloadBytes;
            IsHexViewerVisible = true;
            statusMessage = $"Hex view loaded ({payloadBytes.Length} bytes)";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Error loading hex view: {ex.Message}";
            ClearHexView();
            return false;
        }
    }

    /// <inheritdoc/>
    public void ClearHexView()
    {
        IsHexViewerVisible = false;
        HexPayloadBytes = null;
    }
}
