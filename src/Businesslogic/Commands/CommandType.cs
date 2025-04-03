namespace CrowsNestMqtt.Businesslogic.Commands;

/// <summary>
/// Represents the types of commands that can be parsed from user input.
/// </summary>
public enum CommandType
{
    /// <summary> Represents a search term, not a command. </summary>
    Search,
    /// <summary> Connect to an MQTT broker. </summary>
    Connect,
    /// <summary> Disconnect from the current MQTT broker. </summary>
    Disconnect,
    /// <summary> Publish a message to a topic. </summary>
    Publish,
    /// <summary> Subscribe to a topic filter. </summary>
    Subscribe,
    /// <summary> Unsubscribe from a topic filter. </summary>
    Unsubscribe,
    /// <summary> Export messages to a file. </summary>
    Export,
    /// <summary> Filter messages based on a regex pattern. </summary>
    Filter,
    /// <summary> Clear all displayed messages. </summary>
    ClearMessages,
    /// <summary> Show diagnostic information. </summary>
    ShowDiagnostics,
    /// <summary> Display help information. </summary>
    Help,
    /// <summary> Copy selected messages to the clipboard. </summary>
    Copy,
    /// <summary> Represents an unrecognized or invalid command. </summary>
    Unknown
}