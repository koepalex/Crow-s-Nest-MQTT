using System;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Services
{
    public class ResponseNavigationServiceUnitTests
    {
        private readonly IMessageCorrelationService _mockCorrelationService;
        private readonly ITopicSubscriptionService _mockSubscriptionService;
        private readonly IUINavigationService _mockUINavigationService;

        public ResponseNavigationServiceUnitTests()
        {
            _mockCorrelationService = Substitute.For<IMessageCorrelationService>();
            _mockSubscriptionService = Substitute.For<ITopicSubscriptionService>();
            _mockUINavigationService = Substitute.For<IUINavigationService>();
        }

        private ResponseNavigationService CreateService()
        {
            return new ResponseNavigationService(
                _mockCorrelationService,
                _mockSubscriptionService,
                _mockUINavigationService);
        }

        [Fact]
        public void Constructor_WithNullCorrelationService_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ResponseNavigationService(null!, _mockSubscriptionService, _mockUINavigationService));
        }

        [Fact]
        public void Constructor_WithNullSubscriptionService_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ResponseNavigationService(_mockCorrelationService, null!, _mockUINavigationService));
        }

        [Fact]
        public void Constructor_WithNullUINavigationService_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ResponseNavigationService(_mockCorrelationService, _mockSubscriptionService, null!));
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithNullRequestId_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.NavigateToResponseAsync(null!);
            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithEmptyRequestId_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.NavigateToResponseAsync("");
            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithNoCorrelation_ShouldReturnError()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.NoCorrelationData, result.ErrorType);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithUnsubscribedTopic_ShouldReturnError()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(false);

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.ResponseTopicNotSubscribed, result.ErrorType);
            Assert.Equal("response/topic", result.SelectedTopic);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithPendingStatus_ShouldReturnError()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Pending);

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.ResponseNotReceived, result.ErrorType);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithReceivedStatus_ShouldNavigate()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(new[] { "resp-1", "resp-2" });

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.True(result.Success);
            Assert.Equal("resp-2", result.SelectedMessageId);
            Assert.Equal("response/topic", result.SelectedTopic);
            await _mockUINavigationService.Received(1).NavigateToTopicAsync("response/topic");
            await _mockUINavigationService.Received(1).SelectMessageAsync("resp-2");
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithException_ShouldReturnError()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").ThrowsAsync(new InvalidOperationException("Test exception"));

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.TopicNavigationFailed, result.ErrorType);
            Assert.Contains("Test exception", result.ErrorMessage);
        }

        [Fact]
        public async Task NavigateToResponseAsync_ShouldRaiseNavigationCompletedEvent()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(new[] { "resp-1" });

            var service = CreateService();
            var eventRaised = false;
            NavigationCompletedEventArgs? eventArgs = null;

            service.NavigationCompleted += (s, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            await service.NavigateToResponseAsync("req-1");

            Assert.True(eventRaised);
            Assert.NotNull(eventArgs);
            Assert.Equal("req-1", eventArgs.RequestMessageId);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithNullRequestId_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync(null!);
            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithEmptyRequestId_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("");
            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithNoCorrelation_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithUnsubscribedTopic_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(false);

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithPendingStatus_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Pending);

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithReceivedStatus_ShouldReturnTrue()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.True(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithException_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").ThrowsAsync(new InvalidOperationException());

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task GetNavigationCommandAsync_WithNullRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var command = await service.GetNavigationCommandAsync(null!);
            Assert.Null(command);
        }

        [Fact]
        public async Task GetNavigationCommandAsync_WithEmptyRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var command = await service.GetNavigationCommandAsync("");
            Assert.Null(command);
        }

        [Fact]
        public async Task GetNavigationCommandAsync_WithValidRequestId_ShouldReturnCommand()
        {
            var service = CreateService();
            var command = await service.GetNavigationCommandAsync("req-1");
            Assert.Equal(":gotoresponse req-1", command);
        }

        [Fact]
        public async Task RegisterNavigationCommandAsync_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.RegisterNavigationCommandAsync("req-1");
            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithNullCommand_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(null!);
            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithEmptyCommand_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync("");
            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithInvalidFormat_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(":gotoresponse");
            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
            Assert.Contains("Invalid command format", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithValidCommand_ShouldNavigate()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(new[] { "resp-1" });

            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(":gotoresponse req-1");

            Assert.True(result.Success);
            Assert.Equal("resp-1", result.SelectedMessageId);
        }

        [Fact]
        public async Task GetAvailableNavigationCommandsAsync_ShouldReturnEmptyArray()
        {
            var service = CreateService();
            var commands = await service.GetAvailableNavigationCommandsAsync();
            Assert.Empty(commands);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithNavigationDisabledStatus_ShouldReturnError()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.NavigationDisabled);

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.TopicNavigationFailed, result.ErrorType);
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithReceivedButNoResponseIds_ShouldNavigateToTopic()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(Array.Empty<string>());

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.True(result.Success);
            Assert.Null(result.SelectedMessageId);
            Assert.Equal("response/topic", result.SelectedTopic);
            await _mockUINavigationService.Received(1).NavigateToTopicAsync("response/topic");
            await _mockUINavigationService.DidNotReceive().SelectMessageAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task NavigateToResponseAsync_WithHiddenStatus_ShouldReturnTopicNavigationFailed()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Hidden);

            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.TopicNavigationFailed, result.ErrorType);
            Assert.Contains("disabled", result.ErrorMessage);
        }

        [Fact]
        public async Task NavigateToResponseAsync_Success_ShouldSetNavigatedAtAndDuration()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(new[] { "resp-1" });

            var beforeCall = DateTime.UtcNow;
            var service = CreateService();
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.True(result.Success);
            Assert.True(result.NavigatedAt >= beforeCall);
            Assert.True(result.NavigationDuration >= TimeSpan.Zero);
        }

        [Fact]
        public async Task NavigateToResponseAsync_NoCorrelation_ShouldRaiseEventWithCorrectArgs()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            NavigationCompletedEventArgs? eventArgs = null;
            service.NavigationCompleted += (_, e) => eventArgs = e;

            await service.NavigateToResponseAsync("req-1");

            Assert.NotNull(eventArgs);
            Assert.Equal("req-1", eventArgs.RequestMessageId);
            Assert.False(eventArgs.Result.Success);
            Assert.Equal(NavigationError.NoCorrelationData, eventArgs.Result.ErrorType);
            Assert.False(eventArgs.WasCommandTriggered);
            Assert.Null(eventArgs.Command);
        }

        [Fact]
        public async Task NavigateToResponseAsync_UnsubscribedTopic_ShouldRaiseEventWithWasCommandTriggeredFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(false);

            var service = CreateService();
            NavigationCompletedEventArgs? eventArgs = null;
            service.NavigationCompleted += (_, e) => eventArgs = e;

            await service.NavigateToResponseAsync("req-1");

            Assert.NotNull(eventArgs);
            Assert.False(eventArgs.WasCommandTriggered);
            Assert.Equal("req-1", eventArgs.RequestMessageId);
        }

        [Fact]
        public async Task NavigateToResponseAsync_Exception_ShouldRaiseEventWithErrorDetails()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1")
                .ThrowsAsync(new InvalidOperationException("Correlation failed"));

            var service = CreateService();
            NavigationCompletedEventArgs? eventArgs = null;
            service.NavigationCompleted += (_, e) => eventArgs = e;

            await service.NavigateToResponseAsync("req-1");

            Assert.NotNull(eventArgs);
            Assert.Equal("req-1", eventArgs.RequestMessageId);
            Assert.False(eventArgs.Result.Success);
            Assert.Contains("Correlation failed", eventArgs.Result.ErrorMessage);
        }

        [Fact]
        public async Task NavigateToResponseAsync_NoEventSubscribers_ShouldNotThrow()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            // No event handler attached — should not throw
            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task NavigateToResponseAsync_NullAndEmpty_ShouldNotRaiseEvent()
        {
            var service = CreateService();
            var eventRaised = false;
            service.NavigationCompleted += (_, _) => eventRaised = true;

            await service.NavigateToResponseAsync(null!);
            Assert.False(eventRaised);

            await service.NavigateToResponseAsync("");
            Assert.False(eventRaised);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithHiddenStatus_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Hidden);

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task CanNavigateToResponseAsync_WithNavigationDisabledStatus_ShouldReturnFalse()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.NavigationDisabled);

            var service = CreateService();
            var result = await service.CanNavigateToResponseAsync("req-1");

            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithWhitespaceOnlyCommand_ShouldReturnError()
        {
            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync("   ");

            Assert.False(result.Success);
            Assert.Equal(NavigationError.RequestNotFound, result.ErrorType);
            Assert.Contains("Invalid command format", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithSingleWordCommand_ShouldReturnInvalidFormat()
        {
            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(":unknowncommand");

            Assert.False(result.Success);
            Assert.Contains("Invalid command format", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithExtraWhitespace_ShouldParseCorrectly()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Received);
            _mockCorrelationService.GetResponseMessageIdsAsync("req-1").Returns(new[] { "resp-1" });

            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync("  :gotoresponse   req-1  ");

            Assert.True(result.Success);
            Assert.Equal("resp-1", result.SelectedMessageId);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithExtraArguments_ShouldUseSecondTokenAsMessageId()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(":gotoresponse req-1 extra-arg");

            // Should delegate to NavigateToResponseAsync("req-1"), which fails with no correlation
            Assert.False(result.Success);
            Assert.Equal(NavigationError.NoCorrelationData, result.ErrorType);
        }

        [Fact]
        public async Task ExecuteNavigationCommandAsync_WithNonGotoResponseVerb_StillUsesSecondTokenAsId()
        {
            _mockCorrelationService.GetResponseTopicAsync("msg-42").Returns(Task.FromResult<string?>(null));

            var service = CreateService();
            var result = await service.ExecuteNavigationCommandAsync(":someother msg-42");

            // The service doesn't validate the command verb, it just uses parts[1]
            Assert.False(result.Success);
            Assert.Equal(NavigationError.NoCorrelationData, result.ErrorType);
        }

        [Fact]
        public async Task RegisterNavigationCommandAsync_WithNullRequestId_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.RegisterNavigationCommandAsync(null!);
            Assert.False(result);
        }

        [Fact]
        public async Task RegisterNavigationCommandAsync_WithEmptyRequestId_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.RegisterNavigationCommandAsync("");
            Assert.False(result);
        }

        [Fact]
        public async Task RegisterNavigationCommandAsync_WithCustomCommandText_ShouldReturnFalse()
        {
            var service = CreateService();
            var result = await service.RegisterNavigationCommandAsync("req-1", ":customcommand req-1");
            Assert.False(result);
        }

        [Fact]
        public async Task GetNavigationCommandAsync_WithValidId_ShouldContainGotoResponse()
        {
            var service = CreateService();
            var command = await service.GetNavigationCommandAsync("msg-42");

            Assert.NotNull(command);
            Assert.StartsWith(":gotoresponse", command);
            Assert.Contains("msg-42", command);
        }

        [Fact]
        public async Task GetAvailableNavigationCommandsAsync_ShouldReturnNonNullEmptyArray()
        {
            var service = CreateService();
            var commands = await service.GetAvailableNavigationCommandsAsync();

            Assert.NotNull(commands);
            Assert.IsType<NavigationCommand[]>(commands);
            Assert.Empty(commands);
        }

        [Fact]
        public async Task NavigateToResponseAsync_PendingStatus_ShouldRaiseEventAndSetTopic()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.Pending);

            var service = CreateService();
            NavigationCompletedEventArgs? eventArgs = null;
            service.NavigationCompleted += (_, e) => eventArgs = e;

            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.Equal("response/topic", result.SelectedTopic);
            Assert.NotNull(eventArgs);
            Assert.Equal("req-1", eventArgs.RequestMessageId);
        }

        [Fact]
        public async Task NavigateToResponseAsync_NavigationDisabled_ShouldRaiseEvent()
        {
            _mockCorrelationService.GetResponseTopicAsync("req-1").Returns("response/topic");
            _mockSubscriptionService.IsTopicSubscribedAsync("response/topic").Returns(true);
            _mockCorrelationService.GetResponseStatusAsync("req-1").Returns(ResponseStatus.NavigationDisabled);

            var service = CreateService();
            NavigationCompletedEventArgs? eventArgs = null;
            service.NavigationCompleted += (_, e) => eventArgs = e;

            var result = await service.NavigateToResponseAsync("req-1");

            Assert.False(result.Success);
            Assert.NotNull(eventArgs);
            Assert.False(eventArgs.WasCommandTriggered);
        }
    }
}
