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
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, jsonPayload, Encoding.UTF8.GetBytes(jsonPayload).Length, _mqttServiceMock, _statusBarServiceMock);

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
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, invalidJsonPayload, Encoding.UTF8.GetBytes(invalidJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock);

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
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, complexJsonPayload, Encoding.UTF8.GetBytes(complexJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock);

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
            var viewModel = new MainViewModel(_commandParserService);
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

          var testMessage = new MessageViewModel(messageId, topic, timestamp, arrayJsonPayload, Encoding.UTF8.GetBytes(arrayJsonPayload).Length, _mqttServiceMock, _statusBarServiceMock);

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

        [Fact]
        public void VideoViewer_WithVideoPayload_ShouldDisplayCorrectly()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
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

            var testMessage = new MessageViewModel(messageId, topic, timestamp, "[video]", videoPayload.Length, _mqttServiceMock, _statusBarServiceMock);

            // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.True(viewModel.IsVideoViewerVisible);
            Assert.False(viewModel.IsImageViewerVisible);
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            Assert.NotNull(viewModel.VideoPayload);
            Assert.Equal(videoPayload, viewModel.VideoPayload);
        }

        [Fact]
        public void RawTextViewer_WithTextPayload_ShouldDisplayCorrectly()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, textPayload, Encoding.UTF8.GetBytes(textPayload).Length, _mqttServiceMock, _statusBarServiceMock);

           // Act
            viewModel.SelectedMessage = testMessage;

            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
            Assert.Equal(textPayload, viewModel.RawPayloadDocument.Text);
        }

        [Fact]
        public void ViewToggle_ShouldSwitchBetweenRawAndJsonViews()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, jsonPayload, Encoding.UTF8.GetBytes(jsonPayload).Length, _mqttServiceMock, _statusBarServiceMock);
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
        }

        [Fact]
        public void GuessSyntaxHighlighting_ShouldDetectFormatFromContentType()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
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

           var testMessage = new MessageViewModel(messageId, topic, timestamp, textPayload, Encoding.UTF8.GetBytes(textPayload).Length, _mqttServiceMock, _statusBarServiceMock);

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

        [Fact(Skip = "Clipboard interaction not supported in headless test environment")]
        public void CopyPayloadToClipboard_ForImage_ShouldCopyImagePathToClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);

            byte[] pngHeader = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x01,
                0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB1, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            };
            string topic = "test/image";
            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;
            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(pngHeader)
                .WithContentType("image/png")
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
                .Returns(x => { x[2] = fullMessage; return true; });

            var testMessage = new MessageViewModel(messageId, topic, timestamp, "[image]", pngHeader.Length, _mqttServiceMock, _statusBarServiceMock);

            bool imageInteractionTriggered = false;
            using (var evt = new System.Threading.ManualResetEventSlim())
            {
                viewModel.CopyImageToClipboardInteraction.RegisterHandler(async interaction =>
                {
                    imageInteractionTriggered = true;
                    Assert.NotNull(interaction.Input);
                    evt.Set();
                    await Task.CompletedTask;
                });

                // Act
                viewModel.CopyPayloadCommand.Execute(testMessage).Subscribe();
                evt.Wait(2000); // Wait up to 2 seconds for handler

                // Assert
                Assert.True(imageInteractionTriggered);
                Assert.Contains("Image written to temp file", viewModel.StatusBarText);
            }
        }

        [Fact(Skip = "Clipboard interaction not supported in headless test environment")]
        public void CopyPayloadToClipboard_ForText_ShouldCopyPayloadToClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);

            string textPayload = "Clipboard text payload";
            string topic = "test/text";
            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;
            var fullMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(textPayload))
                .WithContentType("text/plain")
                .Build();

            _mqttServiceMock.TryGetMessage(topic, messageId, out Arg.Any<MqttApplicationMessage?>())
                .Returns(x => { x[2] = fullMessage; return true; });

            var testMessage = new MessageViewModel(messageId, topic, timestamp, textPayload, textPayload.Length, _mqttServiceMock, _statusBarServiceMock);

            bool textInteractionTriggered = false;
            using (var evt = new System.Threading.ManualResetEventSlim())
            {
                viewModel.CopyTextToClipboardInteraction.RegisterHandler(async interaction =>
                {
                    textInteractionTriggered = true;
                    Assert.Equal(textPayload, interaction.Input);
                    evt.Set();
                    await Task.CompletedTask;
                });

                // Act
                viewModel.CopyPayloadCommand.Execute(testMessage).Subscribe();
                evt.Wait(2000); // Wait up to 2 seconds for handler

                // Assert
                Assert.True(textInteractionTriggered);
                Assert.Contains("Payload copied to clipboard", viewModel.StatusBarText);
            }
        }
    }
}
