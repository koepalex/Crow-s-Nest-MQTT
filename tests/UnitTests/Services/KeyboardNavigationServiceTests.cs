using Avalonia.Input;
using CrowsNestMqtt.UI.Services;
using CrowsNestMQTT.BusinessLogic.Navigation;
using MQTTnet;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

/// <summary>
/// Unit tests for KeyboardNavigationService.
/// Tests FR-008, FR-009, FR-013, FR-014, FR-020, FR-021.
/// </summary>
public class KeyboardNavigationServiceTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.MessageNavigation);
    }

    [Fact]
    public void Constructor_WithNullSearchService_ThrowsArgumentNullException()
    {
        // Arrange
        var messageNav = new MessageNavigationState();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KeyboardNavigationService(null!, messageNav, () => false));
    }

    [Fact]
    public void Constructor_WithNullMessageNavigation_ThrowsArgumentNullException()
    {
        // Arrange
        var searchService = new TopicSearchService(() => []);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KeyboardNavigationService(searchService, null!, () => false));
    }

    [Fact]
    public void Constructor_WithNullSuppressFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var searchService = new TopicSearchService(() => []);
        var messageNav = new MessageNavigationState();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KeyboardNavigationService(searchService, messageNav, null!));
    }

    #endregion

    #region HandleKeyPress Tests - FR-008, FR-009

    [Fact]
    public void HandleKeyPress_NKey_ReturnsTrue()
    {
        // Arrange - FR-008: Navigate next search result with 'n'
        var service = CreateServiceWithActiveSearch();

        // Act
        var handled = service.HandleKeyPress(Key.N, KeyModifiers.None);

        // Assert
        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyPress_ShiftN_ReturnsTrue()
    {
        // Arrange - FR-009: Navigate previous search result with 'N'
        var service = CreateServiceWithActiveSearch();

        // Act
        var handled = service.HandleKeyPress(Key.N, KeyModifiers.Shift);

        // Assert
        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyPress_JKey_ReturnsTrue()
    {
        // Arrange - FR-013: Navigate down messages with 'j'
        var service = CreateServiceWithMessages();

        // Act
        var handled = service.HandleKeyPress(Key.J, KeyModifiers.None);

        // Assert
        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyPress_KKey_ReturnsTrue()
    {
        // Arrange - FR-014: Navigate up messages with 'k'
        var service = CreateServiceWithMessages();

        // Act
        var handled = service.HandleKeyPress(Key.K, KeyModifiers.None);

        // Assert
        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyPress_UnrelatedKey_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var handled = service.HandleKeyPress(Key.A, KeyModifiers.None);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public void HandleKeyPress_NKeyWithCtrl_ReturnsFalse()
    {
        // Arrange - Ctrl+N is not a navigation shortcut
        var service = CreateServiceWithActiveSearch();

        // Act
        var handled = service.HandleKeyPress(Key.N, KeyModifiers.Control);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public void HandleKeyPress_JKeyWithShift_ReturnsFalse()
    {
        // Arrange - Shift+J is not a navigation shortcut
        var service = CreateServiceWithMessages();

        // Act
        var handled = service.HandleKeyPress(Key.J, KeyModifiers.Shift);

        // Assert
        Assert.False(handled);
    }

    #endregion

    #region ShouldSuppressShortcuts Tests - FR-020, FR-021

    [Fact]
    public void ShouldSuppressShortcuts_WhenFuncReturnsTrue_ReturnsTrue()
    {
        // Arrange - FR-021: Suppress shortcuts when command palette active
        var service = CreateServiceWithSuppression(true);

        // Act
        var shouldSuppress = service.ShouldSuppressShortcuts();

        // Assert
        Assert.True(shouldSuppress);
    }

    [Fact]
    public void ShouldSuppressShortcuts_WhenFuncReturnsFalse_ReturnsFalse()
    {
        // Arrange - FR-020: Global shortcuts active when not in text input
        var service = CreateServiceWithSuppression(false);

        // Act
        var shouldSuppress = service.ShouldSuppressShortcuts();

        // Assert
        Assert.False(shouldSuppress);
    }

    [Fact]
    public void HandleKeyPress_WhenShortcutsSuppressed_ReturnsFalse()
    {
        // Arrange
        var service = CreateServiceWithSuppression(true);

        // Act
        var handledN = service.HandleKeyPress(Key.N, KeyModifiers.None);
        var handledJ = service.HandleKeyPress(Key.J, KeyModifiers.None);

        // Assert
        Assert.False(handledN);
        Assert.False(handledJ);
    }

    #endregion

    #region NavigateSearchNext/Previous Tests

    [Fact]
    public void NavigateSearchNext_WithActiveSearch_MovesToNextMatch()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();
        Assert.Equal(0, service.ActiveSearchContext!.CurrentIndex);

        // Act
        service.NavigateSearchNext();

        // Assert
        Assert.Equal(1, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchNext_AtLastMatch_WrapsToFirst()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();

        // Move to last match
        service.NavigateSearchNext(); // 0 -> 1
        service.NavigateSearchNext(); // 1 -> 2 (last)
        Assert.Equal(2, service.ActiveSearchContext!.CurrentIndex);

        // Act
        service.NavigateSearchNext();

        // Assert - wraps to first
        Assert.Equal(0, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchNext_WithNoActiveSearch_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        Assert.Null(service.ActiveSearchContext);

        // Act & Assert - should be no-op
        var exception = Record.Exception(() => service.NavigateSearchNext());
        Assert.Null(exception);
    }

    [Fact]
    public void NavigateSearchNext_WithEmptyResults_DoesNotThrow()
    {
        // Arrange
        var service = CreateServiceWithEmptySearch();
        Assert.NotNull(service.ActiveSearchContext);
        Assert.False(service.ActiveSearchContext!.HasMatches);

        // Act & Assert - should be no-op
        var exception = Record.Exception(() => service.NavigateSearchNext());
        Assert.Null(exception);
    }

    [Fact]
    public void NavigateSearchPrevious_WithActiveSearch_MovesToPreviousMatch()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();
        service.NavigateSearchNext(); // Move to second match
        Assert.Equal(1, service.ActiveSearchContext!.CurrentIndex);

        // Act
        service.NavigateSearchPrevious();

        // Assert
        Assert.Equal(0, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchPrevious_AtFirstMatch_WrapsToLast()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();
        Assert.Equal(0, service.ActiveSearchContext!.CurrentIndex);

        // Act
        service.NavigateSearchPrevious();

        // Assert - wraps to last (index 2)
        Assert.Equal(2, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchPrevious_WithNoActiveSearch_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        Assert.Null(service.ActiveSearchContext);

        // Act & Assert
        var exception = Record.Exception(() => service.NavigateSearchPrevious());
        Assert.Null(exception);
    }

    #endregion

    #region NavigateMessageDown/Up Tests

    [Fact]
    public void NavigateMessageDown_WithMessages_MovesToNextMessage()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        Assert.Equal(0, service.MessageNavigation.SelectedIndex);

        // Act
        service.NavigateMessageDown();

        // Assert
        Assert.Equal(1, service.MessageNavigation.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageDown_AtLastMessage_WrapsToFirst()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        service.NavigateMessageDown(); // 0 -> 1
        service.NavigateMessageDown(); // 1 -> 2 (last)
        Assert.Equal(2, service.MessageNavigation.SelectedIndex);

        // Act
        service.NavigateMessageDown();

        // Assert - wraps to first
        Assert.Equal(0, service.MessageNavigation.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageDown_WithNoMessages_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        Assert.False(service.MessageNavigation.HasMessages);

        // Act & Assert
        var exception = Record.Exception(() => service.NavigateMessageDown());
        Assert.Null(exception);
    }

    [Fact]
    public void NavigateMessageUp_WithMessages_MovesToPreviousMessage()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        service.NavigateMessageDown(); // Move to second
        Assert.Equal(1, service.MessageNavigation.SelectedIndex);

        // Act
        service.NavigateMessageUp();

        // Assert
        Assert.Equal(0, service.MessageNavigation.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageUp_AtFirstMessage_WrapsToLast()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        Assert.Equal(0, service.MessageNavigation.SelectedIndex);

        // Act
        service.NavigateMessageUp();

        // Assert - wraps to last (index 2)
        Assert.Equal(2, service.MessageNavigation.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageUp_WithNoMessages_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        Assert.False(service.MessageNavigation.HasMessages);

        // Act & Assert
        var exception = Record.Exception(() => service.NavigateMessageUp());
        Assert.Null(exception);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void ActiveSearchContext_InitiallyNull()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.Null(service.ActiveSearchContext);
    }

    [Fact]
    public void ActiveSearchContext_ReflectsSearchServiceContext()
    {
        // Arrange
        var service = CreateServiceWithActiveSearch();

        // Act & Assert
        Assert.NotNull(service.ActiveSearchContext);
        Assert.True(service.ActiveSearchContext!.HasMatches);
    }

    [Fact]
    public void MessageNavigation_NeverNull()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service.MessageNavigation);
    }

    #endregion

    #region Integration Tests - Key Press to Navigation

    [Fact]
    public void HandleKeyPress_NKey_InvokesNavigateSearchNext()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();
        var initialIndex = service.ActiveSearchContext!.CurrentIndex;

        // Act
        service.HandleKeyPress(Key.N, KeyModifiers.None);

        // Assert
        Assert.Equal(initialIndex + 1, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void HandleKeyPress_ShiftN_InvokesNavigateSearchPrevious()
    {
        // Arrange
        var service = CreateServiceWithMultipleSearchMatches();
        service.NavigateSearchNext(); // Move to index 1
        var currentIndex = service.ActiveSearchContext!.CurrentIndex;

        // Act
        service.HandleKeyPress(Key.N, KeyModifiers.Shift);

        // Assert
        Assert.Equal(currentIndex - 1, service.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void HandleKeyPress_JKey_InvokesNavigateMessageDown()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        var initialIndex = service.MessageNavigation.SelectedIndex;

        // Act
        service.HandleKeyPress(Key.J, KeyModifiers.None);

        // Assert
        Assert.Equal(initialIndex + 1, service.MessageNavigation.SelectedIndex);
    }

    [Fact]
    public void HandleKeyPress_KKey_InvokesNavigateMessageUp()
    {
        // Arrange
        var service = CreateServiceWithMessages();
        service.NavigateMessageDown(); // Move to index 1
        var currentIndex = service.MessageNavigation.SelectedIndex;

        // Act
        service.HandleKeyPress(Key.K, KeyModifiers.None);

        // Assert
        Assert.Equal(currentIndex - 1, service.MessageNavigation.SelectedIndex);
    }

    #endregion

    #region Helper Methods

    private static KeyboardNavigationService CreateService()
    {
        var searchService = new TopicSearchService(() => []);
        var messageNav = new MessageNavigationState();
        return new KeyboardNavigationService(searchService, messageNav, () => false);
    }

    private static KeyboardNavigationService CreateServiceWithSuppression(bool suppress)
    {
        var searchService = new TopicSearchService(() => []);
        var messageNav = new MessageNavigationState();
        return new KeyboardNavigationService(searchService, messageNav, () => suppress);
    }

    private static KeyboardNavigationService CreateServiceWithActiveSearch()
    {
        var topics = new List<TopicReference>
        {
            new("sensor/temperature", "temperature", Guid.NewGuid()),
            new("sensor/humidity", "humidity", Guid.NewGuid()),
            new("device/status", "status", Guid.NewGuid())
        };
        var searchService = new TopicSearchService(() => topics);
        searchService.ExecuteSearch("sensor"); // Creates active search with 2 matches
        var messageNav = new MessageNavigationState();
        return new KeyboardNavigationService(searchService, messageNav, () => false);
    }

    private static KeyboardNavigationService CreateServiceWithMultipleSearchMatches()
    {
        var topics = new List<TopicReference>
        {
            new("test/topic1", "topic1", Guid.NewGuid()),
            new("test/topic2", "topic2", Guid.NewGuid()),
            new("test/topic3", "topic3", Guid.NewGuid())
        };
        var searchService = new TopicSearchService(() => topics);
        searchService.ExecuteSearch("test"); // Creates active search with 3 matches
        var messageNav = new MessageNavigationState();
        return new KeyboardNavigationService(searchService, messageNav, () => false);
    }

    private static KeyboardNavigationService CreateServiceWithEmptySearch()
    {
        var topics = new List<TopicReference>
        {
            new("sensor/temperature", "temperature", Guid.NewGuid())
        };
        var searchService = new TopicSearchService(() => topics);
        searchService.ExecuteSearch("nonexistent"); // No matches
        var messageNav = new MessageNavigationState();
        return new KeyboardNavigationService(searchService, messageNav, () => false);
    }

    private static KeyboardNavigationService CreateServiceWithMessages()
    {
        var messages = new List<MqttApplicationMessage>
        {
            new MqttApplicationMessageBuilder().WithTopic("test/1").WithPayload("msg1").Build(),
            new MqttApplicationMessageBuilder().WithTopic("test/2").WithPayload("msg2").Build(),
            new MqttApplicationMessageBuilder().WithTopic("test/3").WithPayload("msg3").Build()
        };
        var searchService = new TopicSearchService(() => []);
        var messageNav = new MessageNavigationState(messages);
        return new KeyboardNavigationService(searchService, messageNav, () => false);
    }

    #endregion
}
