# Contract: UI Export Buttons

**Feature**: Export All Messages from Topic History
**Contract Type**: User Interface Behavior
**Version**: 1.0.0
**Date**: 2026-01-21

## Overview

This contract defines the behavior of two new UI elements for export functionality:
1. **Export All Button** - Toolbar button next to delete topic button
2. **Per-Message Export Button** - Button in each message history row

Both UI elements provide command-driven export with clear visual feedback and appropriate enabled/disabled states.

---

## Export All Button

### Location

**Toolbar Position:**
- Grid Row 1 (button bar)
- After "DeleteTopicButton", before status indicators
- Horizontal alignment: Left (in StackPanel with other action buttons)

### Visual Appearance

**Icon:**
```xaml
<StreamGeometry x:Key="export_all_regular">
  <!-- Download/export icon - to be defined -->
  M12 2L12 14M12 14L8 10M12 14L16 10M3 14V18C3 19.1046 3.89543 20 5 20H19C20.1046 20 21 19.1046 21 18V14
</StreamGeometry>
```

**Button Style:**
```xaml
<Button x:Name="ExportAllButton"
        Command="{Binding ExportAllCommand}"
        ToolTip.Tip="Export all messages from selected topic (max 100)"
        IsEnabled="{Binding IsExportAllButtonEnabled}"
        MinHeight="32"
        Padding="12,8"
        VerticalContentAlignment="Center"
        HorizontalContentAlignment="Center">
    <Button.Styles>
        <Style Selector="Button:disabled PathIcon">
            <Setter Property="Foreground" Value="{DynamicResource ButtonForegroundDisabled}"/>
            <Setter Property="Opacity" Value="0.5"/>
        </Style>
    </Button.Styles>
    <PathIcon Data="{StaticResource export_all_regular}" Width="16" Height="16"/>
</Button>
```

### Enabled State Logic

**IsExportAllButtonEnabled Property:**
```csharp
public bool IsExportAllButtonEnabled =>
    SelectedNode != null &&           // Topic selected
    FilteredMessageHistory.Any();      // Messages exist in history
```

**Reactive Binding:**
```csharp
this.WhenAnyValue(
    x => x.SelectedNode,
    x => x.FilteredMessageHistory.Count,
    (node, count) => node != null && count > 0)
.ToProperty(this, x => x.IsExportAllButtonEnabled, out _exportAllButtonEnabled);
```

### Behavior on Click

**Preconditions:**
- Button is enabled (topic selected, messages exist)
- Export settings configured OR user provides parameters

**Action Sequence:**
1. Retrieve most recent 100 messages from `FilteredMessageHistory`
2. Generate filename: `{sanitized_topic}_{timestamp}.{ext}`
3. Determine export path (from Settings or prompt user)
4. Call `ExportAllAsync(messages, format, path)`
5. Update StatusBarText with result

**Postconditions:**
- File created at export path
- StatusBarText shows: "Exported {count} messages to {filename}"
- If count > 100: StatusBarText adds " (limited from {original_count})"
- If error: StatusBarText shows error message

**Edge Cases:**
- Topic has 0 messages: Button disabled (cannot click)
- Topic has 1-100 messages: Exports all
- Topic has > 100 messages: Exports most recent 100, shows warning

---

## Per-Message Export Button

### Location

**ListBox Item Template:**
- In MessageHistoryListBox (Grid Row 4)
- DockPanel layout: Button on right side, before copy button
- Visual position: `[Timestamp/Payload] [Export] [Copy]`

### Visual Appearance

**Button in Row Template:**
```xaml
<ListBox.ItemTemplate>
    <DataTemplate x:DataType="vm:MessageViewModel">
        <DockPanel LastChildFill="True" MinWidth="300">
            <!-- Copy Button (Right Aligned) - Existing -->
            <Button DockPanel.Dock="Right"
                    Command="{Binding $parent[ListBox].DataContext.CopyPayloadCommand}"
                    CommandParameter="{Binding}"
                    ToolTip.Tip="Copy Payload"
                    Padding="4" Margin="5,0,0,0" VerticalAlignment="Center">
                <PathIcon Data="{StaticResource copy_regular}" Width="12" Height="12"/>
            </Button>

            <!-- Export Button (Right Aligned) - NEW -->
            <Button DockPanel.Dock="Right"
                    Command="{Binding $parent[ListBox].DataContext.ExportMessageCommand}"
                    CommandParameter="{Binding}"
                    ToolTip.Tip="Export this message"
                    Padding="4" Margin="5,0,0,0" VerticalAlignment="Center">
                <PathIcon Data="{StaticResource export_regular}" Width="12" Height="12"/>
            </Button>

            <!-- DisplayText (Fills remaining space) -->
            <TextBlock Text="{Binding DisplayText}"
                       TextTrimming="CharacterEllipsis"
                       VerticalAlignment="Center"/>
        </DockPanel>
    </DataTemplate>
</ListBox.ItemTemplate>
```

**Icon:**
- Reuse `export_regular` or `copy_regular` icon (or create similar download icon)
- Size: 12×12 (matches copy button)
- Color: Follows theme (disabled state uses ButtonForegroundDisabled)

### Enabled State Logic

**Always Enabled:**
- Button is always enabled (message exists in row by definition)
- No conditional `IsEnabled` binding needed

### Behavior on Click

**Command Binding:**
```csharp
// In MainViewModel:
public ReactiveCommand<MessageViewModel, Unit> ExportMessageCommand { get; }

// Constructor:
ExportMessageCommand = ReactiveCommand.CreateFromTask<MessageViewModel>(
    async msgVm => await ExecuteExportMessageAsync(msgVm));
```

**Action Sequence:**
```csharp
private async Task ExecuteExportMessageAsync(MessageViewModel msgVm)
{
    // 1. Get full message
    var fullMessage = msgVm.GetFullMessage();
    if (fullMessage == null)
    {
        StatusBarText = "Message no longer available in buffer";
        return;
    }

    // 2. Use settings for format and path (same as :export command)
    string format = Settings.ExportFormat?.ToString() ?? "json";
    string path = Settings.ExportPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // 3. Create exporter
    IMessageExporter exporter = format == "json" ? new JsonExporter() : new TextExporter();

    // 4. Export single message (existing ExportToFile method)
    string? result = await Task.Run(() =>
        exporter.ExportToFile(fullMessage, msgVm.Timestamp, path));

    // 5. Update status
    if (result != null)
    {
        StatusBarText = $"Exported message to {Path.GetFileName(result)}";
        _logger.LogInformation("Exported single message to {Path}", result);
    }
    else
    {
        StatusBarText = "Export failed - check logs";
        _logger.LogError("Failed to export message {MessageId}", msgVm.MessageId);
    }
}
```

**Preconditions:**
- MessageViewModel exists (guaranteed by row template)
- Full message retrievable via `GetFullMessage()` (may fail if evicted from buffer)

**Postconditions:**
- Single message file created using existing export logic
- Filename: `{yyyyMMdd_HHmmssfff}_{sanitized_topic}.{ext}` (existing pattern)
- StatusBarText updated with result

**Edge Cases:**
- Message evicted from buffer: `GetFullMessage()` returns null → show error
- Export path not configured: Use Documents folder
- File write error: Return null, log error, show message

---

## Contract Tests

### Export All Button Tests

#### Test 1: Button Disabled when No Topic Selected
```csharp
[Fact]
public void ExportAllButton_NoTopicSelected_IsDisabled()
{
    // Arrange
    var viewModel = new MainViewModel();
    viewModel.SelectedNode = null;
    viewModel.FilteredMessageHistory.Add(new MessageViewModel(...));

    // Act
    bool isEnabled = viewModel.IsExportAllButtonEnabled;

    // Assert
    Assert.False(isEnabled);
}
```

#### Test 2: Button Disabled when No Messages
```csharp
[Fact]
public void ExportAllButton_NoMessages_IsDisabled()
{
    // Arrange
    var viewModel = new MainViewModel();
    viewModel.SelectedNode = new NodeViewModel { FullPath = "test/topic" };
    // FilteredMessageHistory is empty

    // Act
    bool isEnabled = viewModel.IsExportAllButtonEnabled;

    // Assert
    Assert.False(isEnabled);
}
```

#### Test 3: Button Enabled when Topic and Messages Exist
```csharp
[Fact]
public void ExportAllButton_TopicAndMessagesExist_IsEnabled()
{
    // Arrange
    var viewModel = new MainViewModel();
    viewModel.SelectedNode = new NodeViewModel { FullPath = "test/topic" };
    viewModel.FilteredMessageHistory.Add(new MessageViewModel(...));

    // Act
    bool isEnabled = viewModel.IsExportAllButtonEnabled;

    // Assert
    Assert.True(isEnabled);
}
```

#### Test 4: Click Triggers ExportAllCommand
```csharp
[Fact]
public async Task ExportAllButton_Click_ExecutesExportAllCommand()
{
    // Arrange
    var viewModel = new MainViewModel();
    SetupValidExportState(viewModel);
    bool commandExecuted = false;
    viewModel.ExportAllCommand.Subscribe(_ => commandExecuted = true);

    // Act
    await viewModel.ExportAllCommand.Execute();

    // Assert
    Assert.True(commandExecuted);
    Assert.NotNull(viewModel.StatusBarText);
    Assert.Contains("Exported", viewModel.StatusBarText);
}
```

### Per-Message Export Button Tests

#### Test 5: Button Receives MessageViewModel as Parameter
```csharp
[Fact]
public async Task PerMessageExportButton_Click_PassesCorrectMessageViewModel()
{
    // Arrange
    var viewModel = new MainViewModel();
    var testMessage = new MessageViewModel(Guid.NewGuid(), "test/topic", ...);
    MessageViewModel? receivedParam = null;

    viewModel.ExportMessageCommand = ReactiveCommand.CreateFromTask<MessageViewModel>(
        async msg => { receivedParam = msg; });

    // Act
    await viewModel.ExportMessageCommand.Execute(testMessage);

    // Assert
    Assert.NotNull(receivedParam);
    Assert.Equal(testMessage.MessageId, receivedParam.MessageId);
}
```

#### Test 6: Button Uses Existing Export Logic
```csharp
[Fact]
public async Task PerMessageExportButton_Click_UsesExistingExportToFile()
{
    // Arrange
    var viewModel = new MainViewModel();
    var mockExporter = new Mock<IMessageExporter>();
    mockExporter.Setup(e => e.ExportToFile(It.IsAny<MqttApplicationMessage>(),
                                           It.IsAny<DateTime>(),
                                           It.IsAny<string>()))
                .Returns("/exports/test.json");

    var testMessage = CreateTestMessageViewModel();

    // Act
    await viewModel.ExecuteExportMessageAsync(testMessage);

    // Assert
    mockExporter.Verify(e => e.ExportToFile(
        It.Is<MqttApplicationMessage>(m => m.Topic == testMessage.Topic),
        testMessage.Timestamp,
        It.IsAny<string>()), Times.Once);
}
```

#### Test 7: Button Handles Null GetFullMessage
```csharp
[Fact]
public async Task PerMessageExportButton_MessageEvicted_ShowsError()
{
    // Arrange
    var viewModel = new MainViewModel();
    var evictedMessage = new MessageViewModel(...) { GetFullMessage = () => null };

    // Act
    await viewModel.ExecuteExportMessageAsync(evictedMessage);

    // Assert
    Assert.Contains("no longer available", viewModel.StatusBarText, StringComparison.OrdinalIgnoreCase);
}
```

---

## User Feedback

### StatusBarText Messages

**Export All Success:**
```
"Exported 50 messages to sensors_temperature_20260121_143045.json"
```

**Export All with Limit:**
```
"Exported 100 of 150 messages to sensors_temperature_20260121_143045.json (limit enforced)"
```

**Export All Error:**
```
"Export failed: Access denied to C:\exports"
```

**Per-Message Success:**
```
"Exported message to 20260121_143045123_sensors_temperature.json"
```

**Per-Message Evicted:**
```
"Message no longer available in buffer"
```

### Tooltip Text

**Export All Button:**
- Enabled: "Export all messages from selected topic (max 100)"
- Disabled: (tooltip still shows same text, button grayed out)

**Per-Message Button:**
- Always: "Export this message"

---

## Accessibility

**Keyboard Navigation:**
- Export All Button: Focusable via Tab key
- Per-Message Button: Focusable within ListBox item navigation

**Screen Readers:**
- Export All: "Export all messages from selected topic, button"
- Per-Message: "Export this message, button"

**High Contrast Mode:**
- Disabled state uses theme-aware ButtonForegroundDisabled
- Icon opacity reduced to 0.5 when disabled

---

## Success Criteria

✅ **Export All button** positioned correctly next to delete button
✅ **Export All button** enabled/disabled based on topic selection and message existence
✅ **Export All button** triggers bulk export with proper feedback
✅ **Per-message button** appears in each history row
✅ **Per-message button** passes MessageViewModel as command parameter
✅ **Per-message button** reuses existing single-message export logic
✅ **Status bar feedback** clear and actionable for both buttons
✅ **Tooltip text** descriptive and helpful

---

## Non-Functional Requirements

**Performance:**
- Button click response < 50ms (command dispatch)
- Export execution async (does not block UI)
- ListBox item template render < 16ms per row (60 FPS)

**Usability:**
- Buttons visually consistent with existing toolbar/row buttons
- Clear distinction between "export all" and "export this message"
- Disabled state obvious (grayed out icon)

**Maintainability:**
- Reuses existing ReactiveCommand pattern
- Reuses existing export services (JsonExporter, TextExporter)
- Minimal XAML changes (add buttons to existing templates)

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-21 | Initial contract definition |

---

**Related Contracts:**
- [export-all-command.md](./export-all-command.md) - Command parsing contract
- [export-all-service.md](./export-all-service.md) - Service interface extension
