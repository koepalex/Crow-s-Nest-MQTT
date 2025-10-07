using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class ResponseStatusExtensionsTests
{
    [Theory]
    [InlineData(ResponseStatus.Hidden, false)]
    [InlineData(ResponseStatus.Pending, true)]
    [InlineData(ResponseStatus.Received, true)]
    [InlineData(ResponseStatus.NavigationDisabled, true)]
    public void ShouldShowIcon_ReturnsCorrectValue(ResponseStatus status, bool expectedResult)
    {
        // Act
        var result = status.ShouldShowIcon();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(ResponseStatus.Hidden, false)]
    [InlineData(ResponseStatus.Pending, true)]
    [InlineData(ResponseStatus.Received, true)]
    [InlineData(ResponseStatus.NavigationDisabled, false)]
    public void IsClickable_ReturnsCorrectValue(ResponseStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsClickable();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(ResponseStatus.Hidden, "")]
    [InlineData(ResponseStatus.Pending, "clock")]
    [InlineData(ResponseStatus.Received, "arrow")]
    [InlineData(ResponseStatus.NavigationDisabled, "disabled")]
    public void GetIconType_ReturnsCorrectIcon(ResponseStatus status, string expectedIcon)
    {
        // Act
        var result = status.GetIconType();

        // Assert
        Assert.Equal(expectedIcon, result);
    }

    [Theory]
    [InlineData(ResponseStatus.Hidden)]
    [InlineData(ResponseStatus.Pending)]
    [InlineData(ResponseStatus.Received)]
    [InlineData(ResponseStatus.NavigationDisabled)]
    public void GetTooltipText_ReturnsNonEmptyForAllStatuses(ResponseStatus status)
    {
        // Act
        var result = status.GetTooltipText();

        // Assert
        if (status == ResponseStatus.Hidden)
        {
            Assert.Equal(string.Empty, result);
        }
        else
        {
            Assert.NotEmpty(result);
        }
    }

    [Theory]
    [InlineData(ResponseStatus.Hidden, false)]
    [InlineData(ResponseStatus.Pending, false)]
    [InlineData(ResponseStatus.Received, true)]
    [InlineData(ResponseStatus.NavigationDisabled, false)]
    public void HasResponses_ReturnsCorrectValue(ResponseStatus status, bool expectedResult)
    {
        // Act
        var result = status.HasResponses();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(ResponseStatus.Hidden, false)]
    [InlineData(ResponseStatus.Pending, true)]
    [InlineData(ResponseStatus.Received, false)]
    [InlineData(ResponseStatus.NavigationDisabled, false)]
    public void IsPending_ReturnsCorrectValue(ResponseStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsPending();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void GetTooltipText_Pending_ContainsExpectedText()
    {
        // Act
        var result = ResponseStatus.Pending.GetTooltipText();

        // Assert
        Assert.Contains("no responses yet", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTooltipText_Received_ContainsExpectedText()
    {
        // Act
        var result = ResponseStatus.Received.GetTooltipText();

        // Assert
        Assert.Contains("navigate to response", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTooltipText_NavigationDisabled_ContainsExpectedText()
    {
        // Act
        var result = ResponseStatus.NavigationDisabled.GetTooltipText();

        // Assert
        Assert.Contains("disabled", result, StringComparison.OrdinalIgnoreCase);
    }
}
