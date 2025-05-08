using System;

#if BROWSER
using Microsoft.Extensions.Logging;
#endif

namespace CrowsNestMqtt.Utils;

/// <summary>
/// Provides a static application logger that abstracts the underlying logging mechanism
/// based on the compilation target (WASM/Browser vs. other platforms).
/// </summary>
public static class AppLogger
{
#if BROWSER
    private static Microsoft.Extensions.Logging.ILogger? _msLogger;

    /// <summary>
    /// Initializes the logger for WASM environments.
    /// This method should be called during application startup in the WASM project.
    /// </summary>
    /// <param name="loggerFactory">The logger factory provided by the DI container.</param>
    public static void InitializeWasmLogger(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _msLogger = loggerFactory.CreateLogger("App");
    }
#endif

    /// <summary>
    /// Writes an informational log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Information(string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogInformation(messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Information(messageTemplate, propertyValues);
#endif
    }

    /// <summary>
    /// Writes a warning log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Warning(string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogWarning(messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Warning(messageTemplate, propertyValues);
#endif
    }

    /// <summary>
    /// Writes a warning log message with an exception.
    /// </summary>
    /// <param name="exception">Exception related to the event.</param>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Warning(Exception? exception, string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogWarning(exception, messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Warning(exception, messageTemplate, propertyValues);
#endif
    }

    /// <summary>
    /// Writes an error log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Error(string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogError(messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Error(messageTemplate, propertyValues);
#endif
    }

    /// <summary>
    /// Writes an error log message with an exception.
    /// </summary>
    /// <param name="exception">Exception related to the event.</param>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Error(Exception? exception, string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogError(exception, messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Error(exception, messageTemplate, propertyValues);
#endif
    }

    /// <summary>
    /// Writes a debug log message.
    /// </summary>
    /// <param name="messageTemplate">Message template describing the event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    public static void Debug(string messageTemplate, params object[]? propertyValues)
    {
#if BROWSER
        _msLogger?.LogDebug(messageTemplate, propertyValues ?? Array.Empty<object>());
#else
        Serilog.Log.Debug(messageTemplate, propertyValues);
#endif
    }
}