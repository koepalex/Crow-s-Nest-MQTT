using Xunit;
using CrowsNestMqtt.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test information message - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Information(testMessage);

                // Assert
                var ourMessages = logMessages.Where(m => m.level == "Information" && m.message == testMessage).ToList();
                Assert.Single(ourMessages);
                Assert.Equal("Information", ourMessages[0].level);
                Assert.Equal(testMessage, ourMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test message with {{Parameter}} - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Information(testMessage, "value");

                // Assert - Look for our specific test message
                var ourMessages = logMessages.Where(m => m.level == "Information" && m.message == testMessage).ToList();
                Assert.Single(ourMessages);
                Assert.Equal("Information", ourMessages[0].level);
                Assert.Equal(testMessage, ourMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test warning message - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Warning(testMessage);

                // Assert
                var warningMessages = logMessages.Where(m => m.level == "Warning" && m.message == testMessage).ToList();
                Assert.Single(warningMessages);
                Assert.Equal("Warning", warningMessages[0].level);
                Assert.Equal(testMessage, warningMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test warning with exception - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;
            var exception = new Exception("Test exception");

            try
            {
                // Act
                AppLogger.Warning(exception, testMessage);

                // Assert
                var warningMessages = logMessages.Where(m => m.level == "Warning" && m.message == testMessage).ToList();
                Assert.Single(warningMessages);
                Assert.Equal("Warning", warningMessages[0].level);
                Assert.Equal(testMessage, warningMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test warning with null exception - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Warning(null, testMessage);

                // Assert
                var warningMessages = logMessages.Where(m => m.level == "Warning" && m.message == testMessage).ToList();
                Assert.Single(warningMessages);
                Assert.Equal("Warning", warningMessages[0].level);
                Assert.Equal(testMessage, warningMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test error message - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Error(testMessage);

                // Assert
                var errorMessages = logMessages.Where(m => m.level == "Error" && m.message == testMessage).ToList();
                Assert.Single(errorMessages);
                Assert.Equal("Error", errorMessages[0].level);
                Assert.Equal(testMessage, errorMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test error with exception - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;
            var exception = new InvalidOperationException("Test exception");

            try
            {
                // Act
                AppLogger.Error(exception, testMessage);

                // Assert
                var errorMessages = logMessages.Where(m => m.level == "Error" && m.message == testMessage).ToList();
                Assert.Single(errorMessages);
                Assert.Equal("Error", errorMessages[0].level);
                Assert.Equal(testMessage, errorMessages[0].message);
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
                var debugMessages = logMessages.Where(m => m.level == "Debug" && m.message == testMessage).ToList();
                Assert.Single(debugMessages);
                Assert.Equal("Debug", debugMessages[0].level);
                Assert.Equal(testMessage, debugMessages[0].message);
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
            var uniqueId = Guid.NewGuid().ToString();
            var testMessage = $"Test trace message - {uniqueId}";
            Action<string, string> handler = (level, message) => logMessages.Add((level, message));
            
            AppLogger.OnLogMessage += handler;

            try
            {
                // Act
                AppLogger.Trace(testMessage);

                // Assert
                var traceMessages = logMessages.Where(m => m.level == "Trace" && m.message == testMessage).ToList();
                Assert.Single(traceMessages);
                Assert.Equal("Trace", traceMessages[0].level);
                Assert.Equal(testMessage, traceMessages[0].message);
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
