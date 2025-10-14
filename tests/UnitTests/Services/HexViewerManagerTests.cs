using CrowsNestMqtt.UI.Services;
using System;
using System.Text;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class HexViewerManagerTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var manager = new HexViewerManager();

        // Assert
        Assert.False(manager.IsHexViewerVisible);
        Assert.Null(manager.HexPayloadBytes);
    }

    [Theory]
    [InlineData("application/octet-stream", true)]
    [InlineData("application/pdf", true)]
    [InlineData("application/zip", true)]
    [InlineData("application/gzip", true)]
    [InlineData("application/protobuf", true)]
    [InlineData("application/cbor", true)]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("video/mp4", true)]
    [InlineData("audio/mpeg", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    [InlineData("application/xml", false)]
    [InlineData("text/html", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsBinaryContentType_ReturnsCorrectResult(string? contentType, bool expected)
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act
        var result = manager.IsBinaryContentType(contentType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsBinaryContentType_HandlesCaseInsensitivity()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act & Assert
        Assert.True(manager.IsBinaryContentType("APPLICATION/OCTET-STREAM"));
        Assert.True(manager.IsBinaryContentType("Image/PNG"));
        Assert.True(manager.IsBinaryContentType("VIDEO/MP4"));
    }

    [Fact]
    public void IsBinaryContentType_HandlesContentTypeWithParameters()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act & Assert
        Assert.True(manager.IsBinaryContentType("image/png; charset=utf-8"));
        Assert.True(manager.IsBinaryContentType("application/octet-stream; boundary=something"));
    }

    [Fact]
    public void TryLoadHexView_WithValidBytes_ReturnsTrue()
    {
        // Arrange
        var manager = new HexViewerManager();
        var payload = Encoding.UTF8.GetBytes("Test payload");

        // Act
        var result = manager.TryLoadHexView(payload, out var statusMessage);

        // Assert
        Assert.True(result);
        Assert.True(manager.IsHexViewerVisible);
        Assert.Equal(payload, manager.HexPayloadBytes);
        Assert.Contains("12 bytes", statusMessage);
    }

    [Fact]
    public void TryLoadHexView_WithEmptyBytes_ReturnsFalse()
    {
        // Arrange
        var manager = new HexViewerManager();
        var payload = Array.Empty<byte>();

        // Act
        var result = manager.TryLoadHexView(payload, out var statusMessage);

        // Assert
        Assert.False(result);
        Assert.False(manager.IsHexViewerVisible);
        Assert.Null(manager.HexPayloadBytes);
        Assert.Equal("No payload data to display", statusMessage);
    }

    [Fact]
    public void TryLoadHexView_WithNullBytes_ReturnsFalse()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act
        var result = manager.TryLoadHexView(null!, out var statusMessage);

        // Assert
        Assert.False(result);
        Assert.False(manager.IsHexViewerVisible);
        Assert.Null(manager.HexPayloadBytes);
        Assert.Equal("No payload data to display", statusMessage);
    }

    [Fact]
    public void ClearHexView_ClearsAllState()
    {
        // Arrange
        var manager = new HexViewerManager();
        var payload = Encoding.UTF8.GetBytes("Test");
        manager.TryLoadHexView(payload, out _);

        // Act
        manager.ClearHexView();

        // Assert
        Assert.False(manager.IsHexViewerVisible);
        Assert.Null(manager.HexPayloadBytes);
    }

    [Fact]
    public void HexViewerVisibilityChanged_EventRaisedWhenVisibilityChanges()
    {
        // Arrange
        var manager = new HexViewerManager();
        var eventRaised = false;
        var receivedValue = false;

        manager.HexViewerVisibilityChanged += (sender, isVisible) =>
        {
            eventRaised = true;
            receivedValue = isVisible;
        };

        var payload = Encoding.UTF8.GetBytes("Test");

        // Act
        manager.TryLoadHexView(payload, out _);

        // Assert
        Assert.True(eventRaised);
        Assert.True(receivedValue);
    }

    [Fact]
    public void HexPayloadChanged_EventRaisedWhenPayloadChanges()
    {
        // Arrange
        var manager = new HexViewerManager();
        var eventRaised = false;
        byte[]? receivedPayload = null;

        manager.HexPayloadChanged += (sender, payload) =>
        {
            eventRaised = true;
            receivedPayload = payload;
        };

        var testPayload = Encoding.UTF8.GetBytes("Test");

        // Act
        manager.TryLoadHexView(testPayload, out _);

        // Assert
        Assert.True(eventRaised);
        Assert.Equal(testPayload, receivedPayload);
    }

    [Fact]
    public void HexViewerVisibilityChanged_NotRaisedWhenValueDoesNotChange()
    {
        // Arrange
        var manager = new HexViewerManager();
        var eventCount = 0;

        manager.HexViewerVisibilityChanged += (sender, isVisible) =>
        {
            eventCount++;
        };

        // Act - visibility is already false by default
        manager.ClearHexView(); // Should not raise event since visibility is already false

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void TryLoadHexView_UpdatesExistingPayload()
    {
        // Arrange
        var manager = new HexViewerManager();
        var payload1 = Encoding.UTF8.GetBytes("First");
        var payload2 = Encoding.UTF8.GetBytes("Second");

        // Act
        manager.TryLoadHexView(payload1, out _);
        var result = manager.TryLoadHexView(payload2, out var statusMessage);

        // Assert
        Assert.True(result);
        Assert.Equal(payload2, manager.HexPayloadBytes);
        Assert.True(manager.IsHexViewerVisible);
        Assert.Contains("6 bytes", statusMessage);
    }

    [Fact]
    public void IsBinaryContentType_RecognizesAllDocumentFormats()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act & Assert - Office documents
        Assert.True(manager.IsBinaryContentType("application/vnd.ms-excel"));
        Assert.True(manager.IsBinaryContentType("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.True(manager.IsBinaryContentType("application/vnd.ms-powerpoint"));
        Assert.True(manager.IsBinaryContentType("application/msword"));
        Assert.True(manager.IsBinaryContentType("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    [Fact]
    public void IsBinaryContentType_RecognizesArchiveFormats()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act & Assert
        Assert.True(manager.IsBinaryContentType("application/x-tar"));
        Assert.True(manager.IsBinaryContentType("application/x-7z-compressed"));
        Assert.True(manager.IsBinaryContentType("application/x-rar-compressed"));
    }

    [Fact]
    public void IsBinaryContentType_RecognizesSerializationFormats()
    {
        // Arrange
        var manager = new HexViewerManager();

        // Act & Assert
        Assert.True(manager.IsBinaryContentType("application/x-protobuf"));
        Assert.True(manager.IsBinaryContentType("application/grpc"));
        Assert.True(manager.IsBinaryContentType("application/msgpack"));
        Assert.True(manager.IsBinaryContentType("application/x-msgpack"));
        Assert.True(manager.IsBinaryContentType("application/avro"));
        Assert.True(manager.IsBinaryContentType("application/x-avro"));
    }
}
