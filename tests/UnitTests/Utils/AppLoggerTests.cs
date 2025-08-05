using Xunit;
using CrowsNestMqtt.Utils;
using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.UnitTests.Utils
{
    /// <summary>
    /// Tests for the AppLogger class
    /// </summary>
    public class AppLoggerTests
    {
        [Fact]
        public void AppLogger_Information_WithoutParameters_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Information("Test information message");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Information", logMessages[0].level);
                Assert.Equal("Test information message", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Information_WithParameters_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Information("Test message with {Parameter}", "value");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Information", logMessages[0].level);
                Assert.Equal("Test message with {Parameter}", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Warning_WithoutException_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Warning("Test warning message");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Warning", logMessages[0].level);
                Assert.Equal("Test warning message", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Warning_WithException_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;
            var exception = new Exception("Test exception");

            try
            {
                // Act
                AppLogger.Warning(exception, "Test warning with exception");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Warning", logMessages[0].level);
                Assert.Equal("Test warning with exception", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Warning_WithNullException_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Warning(null, "Test warning with null exception");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Warning", logMessages[0].level);
                Assert.Equal("Test warning with null exception", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Error_WithoutException_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Error("Test error message");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Error", logMessages[0].level);
                Assert.Equal("Test error message", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Error_WithException_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;
            var exception = new InvalidOperationException("Test exception");

            try
            {
                // Act
                AppLogger.Error(exception, "Test error with exception");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Error", logMessages[0].level);
                Assert.Equal("Test error with exception", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Debug_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            var testMessage = $"Test debug message {Guid.NewGuid()}"; // Make message unique
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Debug(testMessage);

                // Assert - Check that our specific message was logged
                Assert.True(logMessages.Any(m => m.level == "Debug" && m.message == testMessage), 
                    $"Expected to find Debug message '{testMessage}' but found: {string.Join(", ", logMessages.Select(m => $"[{m.level}] {m.message}"))}");
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_Trace_FiresEvent()
        {
            // Arrange
            var logMessages = new List<(string level, string message)>();
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Trace("Test trace message");

                // Assert
                Assert.Single(logMessages);
                Assert.Equal("Debug", logMessages[0].level); // Trace maps to Debug level in the event
                Assert.Equal("Test trace message", logMessages[0].message);
            }
            finally
            {
                // Cleanup
                AppLogger.OnLogMessage -= handler;
            }
        }

        [Fact]
        public void AppLogger_NoSubscribers_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() =>
            {
                AppLogger.Information("Test message");
                AppLogger.Warning("Test warning");
                AppLogger.Error("Test error");
                AppLogger.Debug("Test debug");
                AppLogger.Trace("Test trace");
            });

            Assert.Null(exception);
        }

        [Fact]
        public void AppLogger_AllMethods_WithNullParameters_DoNotThrow()
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() =>
            {
                AppLogger.Information("Test {0}", null);
                AppLogger.Warning("Test {0}", null);
                AppLogger.Warning(null, "Test {0}", null);
                AppLogger.Error("Test {0}", null);
                AppLogger.Error(null, "Test {0}", null);
                AppLogger.Debug("Test {0}", null);
                AppLogger.Trace("Test {0}", null);
            });

            Assert.Null(exception);
        }
    }
}
