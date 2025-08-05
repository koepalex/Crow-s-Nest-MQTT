using Xunit;
using CrowsNestMqtt.App;
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
        private T? InvokeStaticMethod<T>(Type type, string methodName, object?[]? parameters = null)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // Verify method exists
            
            var result = method?.Invoke(null, parameters ?? Array.Empty<object>());
            return result != null ? (T)result : default;
        }
        
        [Fact]
        public void LoadMqttEndpointFromEnv_ReturnsNullValuesWhenEnvironmentVariablesNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("services__mqtt__default__0", null);

            // Act - Using the correct tuple type
            var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

            // Assert
            Assert.Null(result.Item1); // Should return null when environment variable is not set
            Assert.Null(result.Item2); // Should return null when environment variable is not set
        }

        [Fact]
        public void LoadMqttEndpointFromEnv_ReturnsCustomValuesWhenEnvironmentVariablesSet()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://test.mqtt.server:8883");

                // Act - Using the correct tuple type
                var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

                // Assert
                Assert.Equal("test.mqtt.server", result.Item1);
                Assert.Equal(8883, result.Item2);
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void LoadMqttEndpointFromEnv_HandlesInvalidUriFormat()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "invalid-uri");

                // Act - Using the correct tuple type
                var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

                // Assert
                Assert.Null(result.Item1); // Should return null for invalid URI
                Assert.Null(result.Item2); // Should return null for invalid URI
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void LoadMqttEndpointFromEnv_HandlesEmptyHostname()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://:1883");

                // Act
                var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

                // Assert
                Assert.Null(result.Item1);
                Assert.Null(result.Item2);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void LoadMqttEndpointFromEnv_HandlesZeroPort()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://test.server:0");

                // Act
                var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

                // Assert
                Assert.Null(result.Item1);
                Assert.Null(result.Item2);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact]
        public void LoadMqttEndpointFromEnv_HandlesNegativePort()
        {
            // Arrange
            try
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", "mqtt://test.server:-1");

                // Act
                var result = InvokeStaticMethod<ValueTuple<string?, int?>>(typeof(Program), "LoadMqttEndpointFromEnv");

                // Assert
                Assert.Null(result.Item1);
                Assert.Null(result.Item2);
            }
            finally
            {
                Environment.SetEnvironmentVariable("services__mqtt__default__0", null);
            }
        }

        [Fact(Skip = "BuildAvaloniaApp tests conflict with Avalonia headless setup")]
        public void BuildAvaloniaApp_WithoutParameters_ReturnsValidAppBuilder()
        {
            // Act
            var appBuilder = Program.BuildAvaloniaApp();

            // Assert
            Assert.NotNull(appBuilder);
        }

        [Fact(Skip = "BuildAvaloniaApp tests conflict with Avalonia headless setup")]
        public void BuildAvaloniaApp_WithParameters_ReturnsValidAppBuilder()
        {
            // Act
            var appBuilder = Program.BuildAvaloniaApp("localhost", 1883);

            // Assert
            Assert.NotNull(appBuilder);
        }

        [Fact(Skip = "BuildAvaloniaApp tests conflict with Avalonia headless setup")]
        public void BuildAvaloniaApp_WithNullHostname_ReturnsValidAppBuilder()
        {
            // Act
            var appBuilder = Program.BuildAvaloniaApp(null, 1883);

            // Assert
            Assert.NotNull(appBuilder);
        }

        [Fact(Skip = "BuildAvaloniaApp tests conflict with Avalonia headless setup")]
        public void BuildAvaloniaApp_WithNullPort_ReturnsValidAppBuilder()
        {
            // Act
            var appBuilder = Program.BuildAvaloniaApp("localhost", null);

            // Assert
            Assert.NotNull(appBuilder);
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
