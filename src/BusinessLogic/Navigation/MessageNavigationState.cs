namespace CrowsNestMQTT.BusinessLogic.Navigation;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MQTTnet;

/// <summary>
/// Tracks current position in message history view for keyboard navigation.
/// Supports wrap-around navigation with 'j' (down) and 'k' (up) keys.
/// Observable properties enable MVVM binding for UI updates.
/// </summary>
public sealed class MessageNavigationState : INotifyPropertyChanged
{
    private IReadOnlyList<MqttApplicationMessage> _messages;
    private int _selectedIndex;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the current message list (read-only reference to existing data).
    /// </summary>
    public IReadOnlyList<MqttApplicationMessage> Messages
    {
        get => _messages;
        private set
        {
            if (_messages != value)
            {
                _messages = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the index of the currently selected message.
    /// Returns -1 if no messages exist.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            // Validate index bounds
            if (Messages.Count == 0)
            {
                if (_selectedIndex != -1)
                {
                    _selectedIndex = -1;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMessages));
                }
            }
            else if (value < 0 || value >= Messages.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"SelectedIndex must be between 0 and {Messages.Count - 1}, or -1 if no messages."
                );
            }
            else
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMessages));
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any messages exist for navigation.
    /// </summary>
    public bool HasMessages => Messages.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageNavigationState"/> class.
    /// </summary>
    public MessageNavigationState()
        : this(Array.Empty<MqttApplicationMessage>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageNavigationState"/> class.
    /// </summary>
    /// <param name="messages">Initial message list</param>
    public MessageNavigationState(IReadOnlyList<MqttApplicationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        _messages = messages;
        _selectedIndex = messages.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Navigates to the next message in the history (down).
    /// Wraps to the first message when at the end.
    /// No-op if no messages exist.
    /// </summary>
    public void MoveDown()
    {
        if (Messages.Count == 0)
        {
            return; // No-op
        }

        // Wrap-around: (current + 1) % count
        SelectedIndex = (SelectedIndex + 1) % Messages.Count;
    }

    /// <summary>
    /// Navigates to the previous message in the history (up).
    /// Wraps to the last message when at the start.
    /// No-op if no messages exist.
    /// </summary>
    public void MoveUp()
    {
        if (Messages.Count == 0)
        {
            return; // No-op
        }

        // Wrap-around: (current - 1 + count) % count
        SelectedIndex = (SelectedIndex - 1 + Messages.Count) % Messages.Count;
    }

    /// <summary>
    /// Gets the currently selected message.
    /// </summary>
    /// <returns>The message at SelectedIndex, or null if no messages</returns>
    public MqttApplicationMessage? GetSelectedMessage()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Messages.Count)
        {
            return null;
        }

        return Messages[SelectedIndex];
    }

    /// <summary>
    /// Updates the message list when the topic changes.
    /// Resets SelectedIndex to 0 if new messages exist, -1 otherwise.
    /// </summary>
    /// <param name="messages">The new message list</param>
    public void UpdateMessages(IReadOnlyList<MqttApplicationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Messages = messages;
        SelectedIndex = messages.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
