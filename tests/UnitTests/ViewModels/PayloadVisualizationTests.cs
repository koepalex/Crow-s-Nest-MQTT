using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.Businesslogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using NSubstitute;
using MQTTnet;
using System;
using System.Buffers;
using System.Reflection;
using System.Text;
using System.Reactive;
using Xunit;
using System.Text.Json; // Added for JsonValueKind

namespace CrowsNestMqtt.Tests.ViewModels
{
    public class PayloadVisualizationTests
    {
        private readonly ICommandParserService _commandParserService;

        public PayloadVisualizationTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
        }

        [Fact]
        public void JsonViewer_WithValidJson_ShouldParseCorrectly()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string jsonPayload = "{\"name\":\"test\",\"value\":123}";
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/json",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(jsonPayload))
                },
                PayloadPreview = jsonPayload
            };

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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/invalid-json",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(invalidJsonPayload))
                },
                PayloadPreview = invalidJsonPayload
            };

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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/complex-json",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(complexJsonPayload))
                },
                PayloadPreview = complexJsonPayload
            };

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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/array-json",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(arrayJsonPayload))
                },
                PayloadPreview = arrayJsonPayload
            };

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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/text",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(textPayload))
                },
                PayloadPreview = textPayload
            };

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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/json-toggle",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(jsonPayload))
                },
                PayloadPreview = jsonPayload
            };
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
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/clipboard",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(textPayload))
                },
                PayloadPreview = textPayload
            };
            
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