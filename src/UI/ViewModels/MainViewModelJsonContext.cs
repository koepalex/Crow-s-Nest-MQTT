using System.Text.Json; // Added for JSON formatting
using System.Text.Json.Serialization; // Added for JSON formatting options

namespace CrowsNestMqtt.UI.ViewModels;

// Define the JsonSerializerContext for MainViewModel
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JsonElement))]
internal partial class MainViewModelJsonContext : JsonSerializerContext
{
}
