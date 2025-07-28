namespace CrowsNestMqtt.BusinessLogic.Commands;

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
    /// <summary> Export messages to a file. </summary>
    Export,
    /// <summary> Filter messages based on a regex pattern. </summary>
    Filter,
    /// <summary> Clear all displayed messages. </summary>
    Clear,
    /// <summary> Show diagnostic information. </summary>
    Help,
    /// <summary> Copy selected messages to the clipboard. </summary>
    Copy,
    /// <summary> Pause adding new messages. </summary>
    Pause,
    /// <summary> Resume adding new messages. </summary>
    Resume,
    /// <summary> Expand all nodes in the topic tree. </summary>
    Expand,
    /// <summary> Collapse all nodes in the topic tree. </summary>
    Collapse,
    /// <summary> View the raw payload as text. </summary>
    ViewRaw,
    /// <summary> View the payload as a JSON tree. </summary>
    ViewJson,
    /// <summary> Toggles the settings pane. </summary>
    Settings,
    /// <summary> Set the MQTT username. </summary>
    SetUser,
    /// <summary> Set the MQTT password. </summary>
    SetPassword,
    /// <summary> Set the MQTT authentication mode. </summary>
    SetAuthMode,
    /// <summary> Set the MQTT authentication method. </summary>
    SetAuthMethod,
    /// <summary> Set the MQTT authentication data. </summary>
    SetAuthData,
    /// <summary> Represents an unrecognized or invalid command. </summary>
    Unknown
}
