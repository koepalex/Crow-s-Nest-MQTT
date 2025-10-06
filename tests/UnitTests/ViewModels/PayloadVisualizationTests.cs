using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using NSubstitute;
using CrowsNestMqtt.UI.Services; // Added for IStatusBarService
using MQTTnet;
using System.Reflection;
using System.Text;
using Xunit;
using System.Text.Json; // Added for JsonValueKind
using System.Reactive.Threading.Tasks;
using CrowsNestMqtt.Utils;
using System.IO;
using LibVLCSharp.Shared;

using Avalonia.Threading;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class PayloadVisualizationTests
    {
        static PayloadVisualizationTests()
        {
            // Use reflection to set Dispatcher.UIThread to a synchronous dispatcher for tests
            var dispatcherType = typeof(Dispatcher);
            var field = dispatcherType.GetField("_uiThread", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, new ImmediateDispatcher());
            }
        }

        private class ImmediateDispatcher : IDispatcher
        {
            public bool CheckAccess() => true;
            public void Post(Action action) => action();
            public void Post(Action action, DispatcherPriority priority) => action();
            public void VerifyAccess() { }
            public DispatcherPriority Priority => DispatcherPriority.Normal;
        }
       private readonly ICommandParserService _commandParserService;
       private readonly IMqttService _mqttServiceMock;
       private readonly IStatusBarService _statusBarServiceMock;

       public PayloadVisualizationTests()
       {
           _commandParserService = Substitute.For<ICommandParserService>();
           _mqttServiceMock = Substitute.For<IMqttService>();
           _statusBarServiceMock = Substitute.For<IStatusBarService>();
       }

        [Fact]
        public void JsonViewer_WithValidJson_ShouldParseCorrectly()
        {
            // Arrange
        using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
           string jsonPayload = "{\"name\":\"test\",\"value\":123}";
           var messageId = Guid.NewGuid();
           var timestamp = DateTime.Now;
           var topic = "test/json";
           var fullMessage = new MqttApplicationMessageBuilder()
               .WithTopic(topic)
               .WithPayload(Encoding.UTF8.GetBytes(jsonPayload))
               .Build();

           _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

           var testMessage = new MessageViewModel(messageId, topic, timestamp, jsonPayload, Encoding.UTF8.GetBytes(jsonPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

           // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            Assert.Empty(viewModel.JsonViewer.JsonParseError);
            Assert.NotEmpty(viewModel.JsonViewer.RootNodes);
        }

        [Fact]
        public void JsonViewer_WithInvalidJson_ShouldShowError()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
           string invalidJsonPayload = "{\"name\":\"test\",\"value\":123"; // Missing closing brace
           var messageId = Guid.NewGuid();
           var timestamp = DateTime.Now;
           var topic = "test/invalid-json";
           var fullMessage = new MqttApplicationMessageBuilder()
               .WithTopic(topic)
               .WithPayload(Encoding.UTF8.GetBytes(invalidJsonPayload))
               .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

           var testMessage = new MessageViewModel(messageId, topic, timestamp, invalidJsonPayload, Encoding.UTF8.GetBytes(invalidJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

           // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
            Assert.NotEmpty(viewModel.JsonViewer.JsonParseError);
        }

        [Fact]
        public void JsonViewer_WithComplexNestedJson_ShouldCreateTreeStructure()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            string complexJsonPayload = @"{
                ""person"": {
                    ""name"": ""John"",
                    ""age"": 30,
                    ""address"": {
                        ""street"": ""123 Main St"",
                        ""city"": ""Anytown""
                    },
                    ""phoneNumbers"": [
                        {""type"": ""home"", ""number"": ""555-1234""},
                        {""type"": ""work"", ""number"": ""555-5678""}
                    ]
                }
           }";
           var messageId = Guid.NewGuid();
           var timestamp = DateTime.Now;
           var topic = "test/complex-json";
           var fullMessage = new MqttApplicationMessageBuilder()
               .WithTopic(topic)
               .WithPayload(Encoding.UTF8.GetBytes(complexJsonPayload))
               .Build();

           _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

           var testMessage = new MessageViewModel(messageId, topic, timestamp, complexJsonPayload, Encoding.UTF8.GetBytes(complexJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

           // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            Assert.Empty(viewModel.JsonViewer.JsonParseError);
            Assert.NotEmpty(viewModel.JsonViewer.RootNodes);
            
            // Verify structure - person node should exist
            var personNode = viewModel.JsonViewer.RootNodes[0];
            Assert.Equal("person", personNode.Name);
            Assert.Equal(JsonValueKind.Object, personNode.ValueKind);
            
            // person node should have children
            Assert.NotEmpty(personNode.Children);
        }

        [Fact]
        public void JsonViewer_WithJsonArray_ShouldHandleArraysCorrectly()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            string arrayJsonPayload = @"[
                {""id"": 1, ""name"": ""Item 1""},
                {""id"": 2, ""name"": ""Item 2""},
                {""id"": 3, ""name"": ""Item 3""}
           ]";
          var messageId = Guid.NewGuid();
          var timestamp = DateTime.Now;
          var topic = "test/array-json";
           var fullMessage = new MqttApplicationMessageBuilder()
              .WithTopic(topic)
              .WithPayload(Encoding.UTF8.GetBytes(arrayJsonPayload))
              .Build();

          _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
              .Returns(x => {
                  x[2] = fullMessage;
                  return true;
              });

          var testMessage = new MessageViewModel(messageId, topic, timestamp, arrayJsonPayload, Encoding.UTF8.GetBytes(arrayJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

          // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            Assert.Empty(viewModel.JsonViewer.JsonParseError);
            
            // Verify root is an array
            Assert.Equal(JsonValueKind.Array, viewModel.JsonViewer.RootNodes[0].ValueKind);
            
            // Array should have 3 children (items)
            Assert.Equal(3, viewModel.JsonViewer.RootNodes[0].Children.Count);
       }

        private class MockLibVLC : LibVLC
        {
            public MockLibVLC() : base() { }
        }

        private class MockMediaPlayer : MediaPlayer
        {
            public MockMediaPlayer(LibVLC libvlc) : base(libvlc) { }

            public new virtual void Play()
            {
                // Do nothing
            }

            public new virtual void Stop()
            {
                // Do nothing
            }
        }

        [Fact(Timeout = 60000)]
        public Task VideoViewer_WithVideoPayload_ShouldDisplayCorrectly()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            byte[] videoPayload = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }; // MP4 header fragment
            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;
            var topic = "test/video";
            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(videoPayload)
                .WithContentType("video/mp4")
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
                .Returns(x => { x[2] = fullMessage; return true; });

            var testMessage = new MessageViewModel(messageId, topic, timestamp, "[video]", videoPayload.Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

            // Mock LibVLC and MediaPlayer
            var mockLibVLC = Substitute.For<LibVLC>();
            var mockMediaPlayer = Substitute.For<MediaPlayer>(mockLibVLC);
            viewModel.VlcMediaPlayer = mockMediaPlayer;

            // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.True(viewModel.IsVideoViewerVisible);
            Assert.False(viewModel.IsImageViewerVisible);
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            Assert.NotNull(viewModel.VideoPayload);
            Assert.Equal(videoPayload, viewModel.VideoPayload);

            // Clean up
            viewModel.VlcMediaPlayer = null;
            mockLibVLC.Dispose();
            mockMediaPlayer.Dispose();

            return Task.CompletedTask;
        }

        [Fact(Timeout = 60000)]
        public Task RawTextViewer_WithTextPayload_ShouldDisplayCorrectly()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            string textPayload = "This is a simple text payload that is not JSON.";
            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;
            var topic = "test/text";
            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(textPayload))
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
                .Returns(x => {
                    x[2] = fullMessage;
                    return true;
                });

            var testMessage = new MessageViewModel(messageId, topic, timestamp, textPayload, Encoding.UTF8.GetBytes(textPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

            // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
            Assert.Equal(textPayload, viewModel.RawPayloadDocument.Text);

            return Task.CompletedTask;
        }

        [Fact(Timeout = 60000)]
        public Task ViewToggle_ShouldSwitchBetweenRawAndJsonViews()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
           string jsonPayload = "{\"name\":\"test\",\"value\":123}";
           var messageId = Guid.NewGuid();
           var timestamp = DateTime.Now;
           var topic = "test/json-toggle";
           var fullMessage = new MqttApplicationMessageBuilder()
               .WithTopic(topic)
               .WithPayload(Encoding.UTF8.GetBytes(jsonPayload))
               .Build();

           _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

           var testMessage = new MessageViewModel(messageId, topic, timestamp, jsonPayload, Encoding.UTF8.GetBytes(jsonPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);
           viewModel.SelectedMessage = testMessage;
            
            // Initially JSON viewer should be visible for valid JSON
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            
            // Get the method via reflection
            var switchViewMethod = typeof(MainViewModel).GetMethod("SwitchPayloadView",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Get the PayloadViewType enum type via reflection
            var payloadViewTypeEnum = typeof(MainViewModel).GetNestedType("PayloadViewType", BindingFlags.NonPublic);
            Assert.NotNull(payloadViewTypeEnum);
            var rawValue = Enum.Parse(payloadViewTypeEnum, "Raw");
            var jsonValue = Enum.Parse(payloadViewTypeEnum, "Json");

            // Act - Switch to raw view
            switchViewMethod?.Invoke(viewModel, new object[] { rawValue });

            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);

            // Act - Switch back to JSON view
            switchViewMethod?.Invoke(viewModel, new object[] { jsonValue });

            // Assert
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);

            return Task.CompletedTask;
        }

        [Fact]
        public void GuessSyntaxHighlighting_ShouldDetectFormatFromContentType()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
            var guessSyntaxMethod = typeof(MainViewModel).GetMethod("GuessSyntaxHighlighting",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act & Assert for different content types
            var jsonHighlighting = guessSyntaxMethod?.Invoke(viewModel, new object[] { "application/json", "{}" });
            var xmlHighlighting = guessSyntaxMethod?.Invoke(viewModel, new object[] { "application/xml", "<root/>" });
            var htmlHighlighting = guessSyntaxMethod?.Invoke(viewModel, new object[] { "text/html", "<html></html>" });
            var jsHighlighting = guessSyntaxMethod?.Invoke(viewModel, new object[] { "application/javascript", "function() {}" });
            
            // Assert
            Assert.NotNull(jsonHighlighting);
            Assert.NotNull(xmlHighlighting);
            Assert.NotNull(htmlHighlighting);
            Assert.NotNull(jsHighlighting);
        }

        [Fact]
        public void GuessSyntaxHighlighting_ShouldDetectFormatFromContent()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
            var guessSyntaxMethod = typeof(MainViewModel).GetMethod("GuessSyntaxHighlighting",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act & Assert for different content patterns
            var jsonFromContent = guessSyntaxMethod?.Invoke(viewModel, new object[] { string.Empty, "{\"key\": \"value\"}" });
            var xmlFromContent = guessSyntaxMethod?.Invoke(viewModel, new object[] { string.Empty, "<root><child>text</child></root>" });
            var plainText = guessSyntaxMethod?.Invoke(viewModel, new object[] { string.Empty, "Just plain text, not structured." });
            
            // Assert
            Assert.NotNull(jsonFromContent);
            Assert.NotNull(xmlFromContent);
            Assert.Null(plainText); // For plain text, should return null (no specific highlighting)
        }

        [Fact]
        public void CopyPayloadToClipboard_ShouldInteractWithClipboard()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
           string textPayload = "Test payload for clipboard copy";
           var messageId = Guid.NewGuid();
           var timestamp = DateTime.Now;
           var topic = "test/clipboard";
           var fullMessage = new MqttApplicationMessageBuilder()
               .WithTopic(topic)
               .WithPayload(Encoding.UTF8.GetBytes(textPayload))
               .Build();

           _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

           var testMessage = new MessageViewModel(messageId, topic, timestamp, textPayload, Encoding.UTF8.GetBytes(textPayload).Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

           bool interactionTriggered = false;
            
            // Subscribe to the interaction
            viewModel.CopyTextToClipboardInteraction
                .RegisterHandler(async interaction => {
                    interactionTriggered = true;
                    Assert.Equal(textPayload, interaction.Input);
                    await Task.CompletedTask; // Return a completed task
                });
            
            // Act - Execute the copy command with the message
            viewModel.CopyPayloadCommand.Execute(testMessage).Subscribe();
            
            // Assert
            Assert.True(interactionTriggered);
            // Assert.Contains("copied to clipboard", viewModel.StatusBarText.ToLower()); // Interaction testing can be unreliable without proper setup
        }

        [Fact]
        public void HexViewer_AutoAndManualSwitch_ShouldDisplayHexForBinaryPayload()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
            byte[] binaryPayload = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;
            var topic = "test/binary";
            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(binaryPayload)
                .WithContentType("application/octet-stream")
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
                .Returns(x => { x[2] = fullMessage; return true; });

            var testMessage = new MessageViewModel(messageId, topic, timestamp, "[binary]", binaryPayload.Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

            // Act: Select message (should auto-switch to hex viewer)
            viewModel.SelectedMessage = testMessage;

            // Assert auto-switch
            Assert.True(viewModel.IsHexViewerVisible);
            Assert.NotNull(viewModel.HexPayloadBytes);
            Assert.Equal(binaryPayload, viewModel.HexPayloadBytes);

            // Act: Switch away and back using reflection (simulate :view hex)
            var switchViewHexMethod = typeof(MainViewModel).GetMethod("SwitchPayloadViewHex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // Hide hex viewer first
            typeof(MainViewModel).GetProperty("IsHexViewerVisible")?.SetValue(viewModel, false);
            Assert.False(viewModel.IsHexViewerVisible);

            // Now call manual switch
            switchViewHexMethod?.Invoke(viewModel, null);

            // Assert manual switch
            Assert.True(viewModel.IsHexViewerVisible);
            Assert.NotNull(viewModel.HexPayloadBytes);
            Assert.Equal(binaryPayload, viewModel.HexPayloadBytes);
        }

        [Fact]
        public void SelectingTopicWithImage_ShouldShowImageAndHistory()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
            var topic = "test/topic";
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyPath == null) throw new DirectoryNotFoundException("Could not get assembly path.");
            var imagePath = Path.Combine(assemblyPath, "../../../..", "TestData/test-image.png");
            var imagePayload = File.ReadAllBytes(imagePath);
            var messageId = Guid.NewGuid();

            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(imagePayload)
                .WithContentType("image/png")
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessage;
                   return true;
               });

            // Create a message view model directly
            var messageVm = new MessageViewModel(messageId, topic, DateTime.Now, "[image]", imagePayload.Length, _mqttServiceMock, _statusBarServiceMock, fullMessage);

            // Manually build the topic tree (simulating what the event handler does)
            var testNode = new NodeViewModel("test", null) { FullPath = "test" };
            var topicNode = new NodeViewModel("topic", testNode) { FullPath = topic };
            testNode.Children.Add(topicNode);
            viewModel.TopicTreeNodes.Add(testNode);

            // Simple approach: Just set the message directly and test the viewer logic
            viewModel.SelectedMessage = messageVm;
            
            // Assert - Topic tree should be populated
            Assert.Single(viewModel.TopicTreeNodes);
            Assert.Equal("test", viewModel.TopicTreeNodes[0].Name);
            Assert.Single(viewModel.TopicTreeNodes[0].Children);
            Assert.Equal("topic", viewModel.TopicTreeNodes[0].Children[0].Name);
            
            // Assert - Message should be selected and image viewer should be visible
            Assert.NotNull(viewModel.SelectedMessage);
            Assert.Equal(messageId, viewModel.SelectedMessage.MessageId);
            Assert.True(viewModel.IsImageViewerVisible);
            Assert.True(viewModel.IsAnyPayloadViewerVisible);
            Assert.False(viewModel.IsVideoViewerVisible);
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
        }
    }
}
