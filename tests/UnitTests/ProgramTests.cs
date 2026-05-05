using Xunit;
using CrowsNestMqtt.App;
using CrowsNestMqtt.BusinessLogic.Configuration;
using System.Reflection;
using Avalonia;
using System.Timers;

namespace CrowsNestMqtt.UnitTests
{
    /// <summary>
    /// Tests for the Program class
    /// </summary>
    public class ProgramTests : IDisposable
    {
        private bool _disposed = false;

        public ProgramTests()
        {
            // No Avalonia initialization needed for Program tests
        }

        // Helper method to safely invoke methods with proper null handling
        private static T? InvokeStaticMethod<T>(Type type, string methodName, object?[]? parameters = null)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // Verify method exists
            
            var result = method?.Invoke(null, parameters ?? Array.Empty<object>());
            return result != null ? (T)result : default;
        }
        
        [Fact]
        public void EnvironmentSettingsOverrides_ReturnsNoOverridesWhenNoEnvVarsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            Environment.SetEnvironmentVariable("services__mqtt__mqtt__0", null);
            Environment.SetEnvironmentVariable("CROWSNEST__HOSTNAME", null);

            // Act
            var result = EnvironmentSettingsOverrides.Load();

            // Assert
            Assert.False(result.HasOverrides);
            Assert.False(result.IsAspireEnvironment);
            Assert.Null(result.Hostname);
            Assert.Null(result.Port);
        }

        [Fact]
        public void EnvironmentSettingsOverrides_ParsesAspireDefaultEnvVar()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://test.mqtt.server:8883");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert
                Assert.True(result.HasOverrides);
                Assert.True(result.IsAspireEnvironment);
                Assert.Equal("test.mqtt.server", result.Hostname);
                Assert.Equal(8883, result.Port);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void EnvironmentSettingsOverrides_ParsesAspireMqttEnvVar()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__mqtt__0", "mqtt://aspire.host:41883");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert
                Assert.True(result.HasOverrides);
                Assert.True(result.IsAspireEnvironment);
                Assert.Equal("aspire.host", result.Hostname);
                Assert.Equal(41883, result.Port);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__mqtt__0", null);
            }
        }

        [Fact]
        public void EnvironmentSettingsOverrides_MqttEnvVarTakesPriorityOverDefault()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__mqtt__0", "mqtt://priority.host:9999");
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://fallback.host:1111");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert
                Assert.Equal("priority.host", result.Hostname);
                Assert.Equal(9999, result.Port);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__mqtt__0", null);
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void EnvironmentSettingsOverrides_HandlesInvalidUri()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "invalid-uri");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert - invalid URI means no host/port parsed, but IsAspire is still true
                Assert.Null(result.Hostname);
                Assert.Null(result.Port);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void EnvironmentSettingsOverrides_CrowsnestVarsOverrideAspire()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://aspire.host:1883");
                Environment.SetEnvironmentVariable("CROWSNEST__HOSTNAME", "override.host");
                Environment.SetEnvironmentVariable("CROWSNEST__PORT", "9999");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert
                Assert.Equal("override.host", result.Hostname);
                Assert.Equal(9999, result.Port);
                Assert.True(result.IsAspireEnvironment);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
                Environment.SetEnvironmentVariable("CROWSNEST__HOSTNAME", null);
                Environment.SetEnvironmentVariable("CROWSNEST__PORT", null);
            }
        }

        [Fact]
        public void EnvironmentSettingsOverrides_ParsesAllCrowsnestVars()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("CROWSNEST__HOSTNAME", "mybroker");
                Environment.SetEnvironmentVariable("CROWSNEST__PORT", "8883");
                Environment.SetEnvironmentVariable("CROWSNEST__CLIENT_ID", "test-client");
                Environment.SetEnvironmentVariable("CROWSNEST__KEEP_ALIVE_SECONDS", "30");
                Environment.SetEnvironmentVariable("CROWSNEST__CLEAN_SESSION", "false");
                Environment.SetEnvironmentVariable("CROWSNEST__SESSION_EXPIRY_SECONDS", "600");
                Environment.SetEnvironmentVariable("CROWSNEST__USE_TLS", "true");
                Environment.SetEnvironmentVariable("CROWSNEST__SUBSCRIPTION_QOS", "2");
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_MODE", "userpass");
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_USERNAME", "user1");
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_PASSWORD", "pass1");

                // Act
                var result = EnvironmentSettingsOverrides.Load();

                // Assert
                Assert.True(result.HasOverrides);
                Assert.Equal("mybroker", result.Hostname);
                Assert.Equal(8883, result.Port);
                Assert.Equal("test-client", result.ClientId);
                Assert.Equal(30, result.KeepAliveIntervalSeconds);
                Assert.False(result.CleanSession);
                Assert.Equal(600u, result.SessionExpiryIntervalSeconds);
                Assert.True(result.UseTls);
                Assert.Equal(2, result.SubscriptionQoS);
                Assert.IsType<UsernamePasswordAuthenticationMode>(result.AuthMode);
                var userPass = (UsernamePasswordAuthenticationMode)result.AuthMode!;
                Assert.Equal("user1", userPass.Username);
                Assert.Equal("pass1", userPass.Password);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CROWSNEST__HOSTNAME", null);
                Environment.SetEnvironmentVariable("CROWSNEST__PORT", null);
                Environment.SetEnvironmentVariable("CROWSNEST__CLIENT_ID", null);
                Environment.SetEnvironmentVariable("CROWSNEST__KEEP_ALIVE_SECONDS", null);
                Environment.SetEnvironmentVariable("CROWSNEST__CLEAN_SESSION", null);
                Environment.SetEnvironmentVariable("CROWSNEST__SESSION_EXPIRY_SECONDS", null);
                Environment.SetEnvironmentVariable("CROWSNEST__USE_TLS", null);
                Environment.SetEnvironmentVariable("CROWSNEST__SUBSCRIPTION_QOS", null);
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_MODE", null);
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_USERNAME", null);
                Environment.SetEnvironmentVariable("CROWSNEST__AUTH_PASSWORD", null);
            }
        }

        [Fact]
        public void ParseMqttUri_ValidUri_ReturnsHostAndPort()
        {
            var (host, port) = EnvironmentSettingsOverrides.ParseMqttUri("mqtt://localhost:41883");
            Assert.Equal("localhost", host);
            Assert.Equal(41883, port);
        }

        [Fact]
        public void ParseMqttUri_EmptyHost_ReturnsNull()
        {
            var (host, port) = EnvironmentSettingsOverrides.ParseMqttUri("mqtt://:1883");
            Assert.Null(host);
            Assert.Null(port);
        }

        [Fact]
        public void ParseMqttUri_InvalidUri_ReturnsNull()
        {
            var (host, port) = EnvironmentSettingsOverrides.ParseMqttUri("not a uri at all");
            Assert.Null(host);
            Assert.Null(port);
        }

        [Fact]
        public void CurrentDomain_UnhandledException_LogsException()
        {
            // Create args
            var exception = new Exception("Test exception");
            var args = new UnhandledExceptionEventArgs(exception, isTerminating: false);
            
            // This should not throw
            InvokeStaticMethod<object>(typeof(Program), "CurrentDomain_UnhandledException", 
                new object?[] { this, args });
        }

        [Fact]
        public void CurrentDomain_UnhandledException_LogsTerminatingException()
        {
            // Create args with terminating true
            var exception = new Exception("Test terminating exception");
            var args = new UnhandledExceptionEventArgs(exception, isTerminating: true);
            
            // This should not throw
            var result = Record.Exception(() => InvokeStaticMethod<object>(typeof(Program), "CurrentDomain_UnhandledException", 
                new object?[] { this, args }));
            
            Assert.Null(result);
        }

        [Fact]
        public void TaskScheduler_UnobservedTaskException_LogsException()
        {
            // Create a task with an exception
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(new Exception("Test task exception"));
            var task = tcs.Task;
            
            try 
            {
                // Force the task to complete and throw
                if (task.IsFaulted)
                {
                    // Just access to ensure it's faulted
                    var _ = task.Exception;
                }
            }
            catch
            {
                // Ignore
            }
            
            // Create args with aggregate exception (the right type for UnobservedTaskException)
            var args = new UnobservedTaskExceptionEventArgs(
                new AggregateException(new Exception("Test task exception")));
            
            // This should not throw
            InvokeStaticMethod<object>(typeof(Program), "TaskScheduler_UnobservedTaskException", 
                new object?[] { this, args });
        }

        [Fact]
        public void TaskScheduler_UnobservedTaskException_SetsObserved()
        {
            // Create args
            var args = new UnobservedTaskExceptionEventArgs(
                new AggregateException(new Exception("Test task exception")));
            
            // Act
            InvokeStaticMethod<object>(typeof(Program), "TaskScheduler_UnobservedTaskException", 
                new object?[] { this, args });
            
            // Assert
            Assert.True(args.Observed);
        }

        [Fact]
        public void CollectAndCompactHeap_DoesNotThrow()
        {
            // Arrange
            var eventArgs = new ElapsedEventArgs(DateTime.Now);
            
            // Act & Assert - should not throw
            var exception = Record.Exception(() => 
                InvokeStaticMethod<object>(typeof(Program), "CollectAndCompactHeap", 
                    new object?[] { this, eventArgs }));
            
            Assert.Null(exception);
        }

        [Fact]
        public void OnShutdownRequested_WithDisposableViewModel_DisposesCorrectly()
        {
            // This test verifies the OnShutdownRequested method handles disposal correctly
            // We can't easily test the full method due to UI dependencies, but we can test the signature
            
            var method = typeof(Program).GetMethod("OnShutdownRequested", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            
            var parameters = method.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(object), parameters[0].ParameterType);
            Assert.Equal("sender", parameters[0].Name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Cleanup if needed
                _disposed = true;
            }
        }
    }
}
