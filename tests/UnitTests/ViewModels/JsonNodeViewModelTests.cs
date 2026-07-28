using System.Text.Json;
using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class JsonNodeViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties_ForString()
    {
        var element = JsonDocument.Parse("\"hello\"").RootElement;
        var vm = new JsonNodeViewModel("key", element, "$.key");

        Assert.Equal("key", vm.Name);
        Assert.Equal("\"hello\"", vm.ValueDisplay);
        Assert.Equal(JsonValueKind.String, vm.ValueKind);
        Assert.Equal("$.key", vm.JsonPath);
        Assert.True(vm.IsValueNode);
        Assert.Empty(vm.Children);
    }

    [Fact]
    public void Constructor_SetsProperties_ForNumber()
    {
        var element = JsonDocument.Parse("123").RootElement;
        var vm = new JsonNodeViewModel("num", element, "$.num");

        Assert.Equal("123", vm.ValueDisplay);
        Assert.Equal(JsonValueKind.Number, vm.ValueKind);
    }

    [Fact]
    public void Constructor_SetsProperties_ForBoolean()
    {
        var elementTrue = JsonDocument.Parse("true").RootElement;
        var vmTrue = new JsonNodeViewModel("bool", elementTrue, "$.bool");
        Assert.Equal("true", vmTrue.ValueDisplay);
        Assert.Equal(JsonValueKind.True, vmTrue.ValueKind);

        var elementFalse = JsonDocument.Parse("false").RootElement;
        var vmFalse = new JsonNodeViewModel("bool", elementFalse, "$.bool");
        Assert.Equal("false", vmFalse.ValueDisplay);
        Assert.Equal(JsonValueKind.False, vmFalse.ValueKind);
    }

    [Fact]
    public void Constructor_SetsProperties_ForNull()
    {
        var element = JsonDocument.Parse("null").RootElement;
        var vm = new JsonNodeViewModel("n", element, "$.n");
        Assert.Equal("null", vm.ValueDisplay);
        Assert.Equal(JsonValueKind.Null, vm.ValueKind);
    }

    [Fact]
    public void Constructor_SetsProperties_ForObject()
    {
        var element = JsonDocument.Parse("{\"a\":1}").RootElement;
        var vm = new JsonNodeViewModel("obj", element, "$.obj");
        Assert.Equal("{...}", vm.ValueDisplay);
        Assert.Equal(JsonValueKind.Object, vm.ValueKind);
        Assert.True(vm.IsValueNode); // Children not populated in constructor
    }

    [Fact]
    public void Constructor_SetsProperties_ForArray()
    {
        var element = JsonDocument.Parse("[1,2,3]").RootElement;
        var vm = new JsonNodeViewModel("arr", element, "$.arr");
        Assert.Equal("[...] (3)", vm.ValueDisplay);
        Assert.Equal(JsonValueKind.Array, vm.ValueKind);
    }
}
