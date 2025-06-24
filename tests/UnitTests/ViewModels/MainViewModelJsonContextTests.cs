using System.Text.Json;
using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class MainViewModelJsonContextTests
    {
        [Fact]
        public void JsonContext_SerializeJsonElement()
        {
            // Arrange
            var element = JsonDocument.Parse("{ \"test\": \"value\" }").RootElement;
            
            // Act
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.TypeInfoResolver = MainViewModelJsonContext.Default;
            var json = JsonSerializer.Serialize(element, options);
            
            // Assert
            Assert.Contains("test", json);
            Assert.Contains("value", json);
        }

        [Fact]
        public void JsonContext_DeserializeJsonElement()
        {
            // Arrange
            string jsonString = "{ \"test\": \"value\" }";
            
            // Act
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.TypeInfoResolver = MainViewModelJsonContext.Default;
            var element = JsonSerializer.Deserialize<JsonElement>(jsonString, options);
            
            // Assert
            Assert.Equal("value", element.GetProperty("test").GetString());
        }
        
        [Fact]
        public void JsonContext_SerializeDeserializeWithTypeInfo()
        {
            // Arrange
            var element = JsonDocument.Parse("{ \"key\": \"value\", \"nested\": { \"inner\": 123 } }").RootElement;
            
            // Act
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.TypeInfoResolver = MainViewModelJsonContext.Default;
            var json = JsonSerializer.Serialize(element, options);
            var deserializedElement = JsonSerializer.Deserialize<JsonElement>(json, options);
            
            // Assert
            Assert.Equal("value", deserializedElement.GetProperty("key").GetString());
            Assert.Equal(123, deserializedElement.GetProperty("nested").GetProperty("inner").GetInt32());
        }
    }
}
