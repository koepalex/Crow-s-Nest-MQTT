using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.UI.Contracts;
using CrowsNestMqtt.UI.ViewModels; // Added for ResponseIconViewModel

namespace CrowsNestMqtt.UI.Services
{
    /// <summary>
    /// Service implementation for managing response status icons in the UI.
    /// Coordinates with MessageCorrelationService for state management.
    /// </summary>
    public class ResponseIconService : IResponseIconService
    {
        private readonly IMessageCorrelationService _correlationService;
        private readonly IResponseNavigationService? _navigationService;
        private readonly ConcurrentDictionary<string, ResponseIconViewModel> _iconViewModels = new();
        private IconConfiguration _configuration = CreateDefaultConfiguration();

        public event EventHandler<IconStatusChangedEventArgs>? IconStatusChanged;
        public event EventHandler<IconClickedEventArgs>? IconClicked;

        public ResponseIconService(
            IMessageCorrelationService correlationService,
            IResponseNavigationService? navigationService)
        {
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
            _navigationService = navigationService; // Navigation service is optional
        }

        public Task<ResponseIconViewModel?> CreateIconViewModelAsync(
            string requestMessageId,
            bool hasResponseTopic,
            bool isResponseTopicSubscribed)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult<ResponseIconViewModel?>(null);

            // Only show icon if message has response-topic and topic is subscribed
            if (!hasResponseTopic || !isResponseTopicSubscribed)
                return Task.FromResult<ResponseIconViewModel?>(null);

            var status = ResponseStatus.Pending; // Default to pending for new requests
            var iconPath = GetIconPathForStatus(status);
            var tooltip = status.GetTooltipText();

            var viewModel = new ResponseIconViewModel
            {
                RequestMessageId = requestMessageId,
                Status = status,
                IconPath = iconPath,
                ToolTip = tooltip,
                IsClickable = status.IsClickable(),
                IsVisible = true,
                LastUpdated = DateTime.UtcNow
            };

            _iconViewModels[requestMessageId] = viewModel;

            return Task.FromResult<ResponseIconViewModel?>(viewModel);
        }

        public async Task<bool> UpdateIconStatusAsync(string requestMessageId, ResponseStatus newStatus)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return false;

            if (!_iconViewModels.TryGetValue(requestMessageId, out var viewModel))
                return false;

            var oldStatus = viewModel.Status;
            if (oldStatus == newStatus)
                return false; // No change needed

            // Update the view model
            viewModel.Status = newStatus;
            viewModel.IconPath = GetIconPathForStatus(newStatus);
            viewModel.ToolTip = newStatus.GetTooltipText();
            viewModel.IsClickable = newStatus.IsClickable();
            viewModel.IsVisible = newStatus.ShouldShowIcon();
            viewModel.LastUpdated = DateTime.UtcNow;

            // Raise status changed event
            IconStatusChanged?.Invoke(this, new IconStatusChangedEventArgs
            {
                RequestMessageId = requestMessageId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                IconPath = viewModel.IconPath,
                ChangedAt = DateTime.UtcNow
            });

            return await Task.FromResult(true);
        }

        public async Task<IconClickResult> HandleIconClickAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
            {
                return new IconClickResult
                {
                    Handled = false,
                    NavigationTriggered = false,
                    ErrorMessage = "Invalid request message ID"
                };
            }

            if (!_iconViewModels.TryGetValue(requestMessageId, out var viewModel))
            {
                return new IconClickResult
                {
                    Handled = false,
                    NavigationTriggered = false,
                    ErrorMessage = "Icon view model not found"
                };
            }

            // Raise clicked event
            IconClicked?.Invoke(this, new IconClickedEventArgs
            {
                RequestMessageId = requestMessageId,
                CurrentStatus = viewModel.Status,
                IsNavigationEnabled = viewModel.IsClickable,
                ClickedAt = DateTime.UtcNow
            });

            // Only navigate if clickable
            if (!viewModel.IsClickable)
            {
                return new IconClickResult
                {
                    Handled = true,
                    NavigationTriggered = false,
                    ErrorMessage = "Navigation is disabled for this message"
                };
            }

            // Trigger navigation via the navigation service if available
            if (_navigationService == null)
            {
                return new IconClickResult
                {
                    Handled = true,
                    NavigationTriggered = false,
                    ErrorMessage = "Navigation service not available",
                    NavigationCommand = $":gotoresponse {requestMessageId}"
                };
            }

            var navigationResult = await _navigationService.NavigateToResponseAsync(requestMessageId);

            var navigationCommand = navigationResult.Success
                ? $":gotoresponse {requestMessageId}"
                : null;

            return new IconClickResult
            {
                Handled = true,
                NavigationTriggered = navigationResult.Success,
                ErrorMessage = navigationResult.Success ? null : navigationResult.ErrorMessage,
                NavigationCommand = navigationCommand
            };
        }

        public Task<ResponseIconViewModel?> GetIconViewModelAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult<ResponseIconViewModel?>(null);

            _iconViewModels.TryGetValue(requestMessageId, out var viewModel);
            return Task.FromResult(viewModel);
        }

        public Task<bool> RemoveIconAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult(false);

            var removed = _iconViewModels.TryRemove(requestMessageId, out _);
            return Task.FromResult(removed);
        }

        public Task<IconConfiguration> GetIconConfigurationAsync()
        {
            return Task.FromResult(_configuration);
        }

        public Task<bool> UpdateIconConfigurationAsync(IconConfiguration configuration)
        {
            if (configuration == null)
                return Task.FromResult(false);

            _configuration = configuration;

            // Update all existing view models with new configuration
            foreach (var viewModel in _iconViewModels.Values)
            {
                viewModel.IconPath = GetIconPathForStatus(viewModel.Status);
            }

            return Task.FromResult(true);
        }

        private string GetIconPathForStatus(ResponseStatus status)
        {
            return status switch
            {
                ResponseStatus.Pending => _configuration.ClockIconPath,
                ResponseStatus.Received => _configuration.ArrowIconPath,
                ResponseStatus.NavigationDisabled => _configuration.DisabledClockIconPath,
                ResponseStatus.Hidden => string.Empty,
                _ => string.Empty
            };
        }

        private static IconConfiguration CreateDefaultConfiguration()
        {
            return new IconConfiguration
            {
                ClockIconPath = "avares://CrowsNestMqtt/Assets/Icons/clock.svg",
                ArrowIconPath = "avares://CrowsNestMqtt/Assets/Icons/arrow.svg",
                DisabledClockIconPath = "avares://CrowsNestMqtt/Assets/Icons/clock_disabled.svg",
                IconColor = "#666666",
                HoverColor = "#333333",
                DisabledColor = "#CCCCCC",
                IconSize = 16.0,
                EnableHoverEffects = true,
                EnableClickAnimation = true
            };
        }
    }
}
