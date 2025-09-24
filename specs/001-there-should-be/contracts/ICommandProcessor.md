# Contract: ICommandProcessor Extension

**Purpose**: Extension to existing command processor for handling `:deletetopic` command

## Command Interface Extension

```csharp
public interface ICommandProcessor
{
    // Existing methods...

    /// <summary>
    /// Processes the :deletetopic command with optional topic pattern
    /// </summary>
    /// <param name="arguments">Command arguments (optional topic pattern)</param>
    /// <param name="cancellationToken">Token for operation cancellation</param>
    /// <returns>Command execution result</returns>
    Task<CommandResult> ExecuteDeleteTopicCommand(string[] arguments, CancellationToken cancellationToken = default);
}
```

## Command Contract

### ExecuteDeleteTopicCommand
**Command Syntax**:
- `:deletetopic` - Delete currently selected topic and subtopics
- `:deletetopic <topic>` - Delete specified topic and subtopics
- `:deletetopic <topic> --confirm` - Skip confirmation prompt

**Preconditions**:
- Arguments array may be empty (uses selected topic)
- If arguments provided, first argument must be valid topic name
- MQTT connection must be active
- Command palette system must be active

**Postconditions**:
- Returns CommandResult indicating success/failure
- Successful execution triggers delete operation asynchronously
- Status bar shows operation start message
- User receives completion notification with summary
- Failed operation shows clear error message

**Argument Processing**:
- Empty arguments: Use currently selected topic from UI
- Single argument: Use as topic pattern
- "--confirm" flag: Skip user confirmation for large operations
- Invalid arguments: Return error result with usage message

## Command Result Contract

```csharp
public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public CommandExecutionContext Context { get; set; }
}
```

### Success Cases
- `Success: true, Message: "Delete operation started for {count} topics"`
- `Success: true, Message: "No retained messages found for topic pattern"`

### Error Cases
- `Success: false, Message: "No topic selected and no pattern provided"`
- `Success: false, Message: "Invalid topic pattern: {pattern}"`
- `Success: false, Message: "MQTT connection not available"`
- `Success: false, Message: "Operation would exceed limit of {limit} topics"`

## Integration Requirements

### UI Command Registration
- Command must be registered with command palette system
- Must support tab completion for topic names
- Must integrate with existing help system (`:help deletetopic`)

### Context Integration
- Must access current topic selection from UI state
- Must validate permissions before starting operation
- Must integrate with existing status bar notification system

### Error Handling Integration
- Uses existing error dialog system for critical failures
- Integrates with application logging framework
- Follows established patterns for async command execution