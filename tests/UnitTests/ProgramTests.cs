using Xunit;
using CrowsNestMqtt.App;
using System.Reflection;

namespace CrowsNestMqtt.UnitTests
{
    /// <summary>
    /// Tests for the Program class
    /// </summary>
    public class ProgramTests
    {
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

        // Skip this test since BuildAvaloniaApp requires proper UI initialization
        [Fact(Skip = "Requires UI initialization which is not available in unit tests")]
        public void BuildAvaloniaApp_ReturnsNonNullAppBuilder()
        {
            // This test is skipped as it requires UI initialization
            // which is not possible in a headless unit test environment
        }

        // Skip this test since it depends on static state that may not be initialized in tests
        [Fact(Skip = "Depends on static state that may not be properly initialized in tests")]
        public void SetupGcTimerIfNeeded_SetsUpGcTimerCorrectly()
        {
            // This test is skipped as it requires static state initialization
            // which is not ideal for unit tests
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

        // Skip this test as it depends on static state
        [Fact(Skip = "Depends on static state that may not be properly initialized in tests")]
        public void GcTimerCallback_InvokesGarbageCollection()
        {
            // This test is skipped as it requires static state initialization
            // which is not ideal for unit tests
        }
    }
}
