namespace CrowsNestMqtt.Utils;

/// <summary>
/// Provides a static application logger that abstracts the underlying logging mechanism.
/// </summary>
public static class AppLogger
{
    /// <summary>
    /// Writes an informational log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Information(string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Information(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Writes a warning log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Warning(string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Warning(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Writes a warning log message with an exception.
    /// </summary>
    /// <param name="exception">Exception related to the event.</param>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Warning(Exception? exception, string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Warning(exception, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Writes an error log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Error(string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Error(messageTemplate, propertyValues);
    }

    /// <summary>
    /// Writes an error log message with an exception.
    /// </summary>
    /// <param name="exception">Exception related to the event.</param>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Error(Exception? exception, string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Error(exception, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Writes a debug log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Debug(string messageTemplate, params object[]? propertyValues)
    {
        Serilog.Log.Debug(messageTemplate, propertyValues);
    }
}