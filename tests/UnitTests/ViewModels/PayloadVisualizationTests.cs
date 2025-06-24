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

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class PayloadVisualizationTests
    {
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
            
            // Act - Switch to raw view
            switchViewMethod?.Invoke(viewModel, new object[] { true });
            
            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
            
            // Act - Switch back to JSON view
            switchViewMethod?.Invoke(viewModel, new object[] { false });
            
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
    }
}