using System.Text.Json;
using Xunit;
using CrowsNestMqtt.UI.Theming;

namespace CrowsNestMqtt.UnitTests.Theming;

public class JsonValueBrushResourcesTests
{
    [Theory]
    [InlineData(JsonValueKind.String, JsonValueBrushResources.SuccessBrushKey)]
    [InlineData(JsonValueKind.True, JsonValueBrushResources.SuccessBrushKey)]
    [InlineData(JsonValueKind.Number, JsonValueBrushResources.AttentionBrushKey)]
    [InlineData(JsonValueKind.False, JsonValueBrushResources.CriticalBrushKey)]
    [InlineData(JsonValueKind.Null, JsonValueBrushResources.TextSecondaryBrushKey)]
    [InlineData(JsonValueKind.Object, JsonValueBrushResources.TextPrimaryBrushKey)]
    [InlineData(JsonValueKind.Array, JsonValueBrushResources.TextPrimaryBrushKey)]
    [InlineData(JsonValueKind.Undefined, JsonValueBrushResources.TextPrimaryBrushKey)]
    public void GetResourceKey_MapsValueKindToSemanticBrushKey(JsonValueKind kind, string expectedKey)
    {
        Assert.Equal(expectedKey, JsonValueBrushResources.GetResourceKey(kind));
    }
}
