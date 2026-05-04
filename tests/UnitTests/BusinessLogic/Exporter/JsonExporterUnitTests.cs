using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MQTTnet;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Exporter;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Exporter
{
    public class JsonExporterUnitTests
    {
        private readonly JsonExporter _exporter = new();

        [Fact]
        public void ExporterType_ShouldBeJson()
        {
            Assert.Equal(ExportTypes.json, _exporter.ExporterType);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithSimpleMessage_ShouldGenerateJson()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithPayload("test payload")
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, isValidUtf8, payloadStr) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.NotEmpty(content);
            Assert.True(isValidUtf8);
            Assert.Equal("test payload", payloadStr);
            Assert.Contains("test/topic", content);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithNoPayload_ShouldHandleGracefully()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, isValidUtf8, payloadStr) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.NotEmpty(content);
            Assert.True(isValidUtf8);
            Assert.Equal("[No Payload]", payloadStr);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithCorrelationData_ShouldIncludeHex()
        {
            var correlationData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithCorrelationData(correlationData)
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, _, _) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.Contains("01020304", content);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithResponseTopic_ShouldInclude()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithResponseTopic("response/topic")
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, _, _) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.Contains("response/topic", content);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithUserProperties_ShouldInclude()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithUserProperty("key1", Encoding.UTF8.GetBytes("value1"))
                .WithUserProperty("key2", Encoding.UTF8.GetBytes("value2"))
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, _, _) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.Contains("key1", content);
            Assert.Contains("value1", content);
            Assert.Contains("key2", content);
            Assert.Contains("value2", content);
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithRetainFlag_ShouldInclude()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithRetainFlag()
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, _, _) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.Contains("true", content.ToLower());
        }

        [Fact]
        public void GenerateDetailedTextFromMessage_WithContentType_ShouldInclude()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithContentType("application/json")
                .Build();
            var receivedTime = DateTime.UtcNow;

            var (content, _, _) = _exporter.GenerateDetailedTextFromMessage(msg, receivedTime);

            Assert.Contains("application/json", content);
        }

        [Fact]
        public void ExportToFile_WithValidMessage_ShouldCreateFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic("test/topic")
                    .WithPayload("test payload")
                    .Build();
                var receivedTime = DateTime.UtcNow;

                var filePath = _exporter.ExportToFile(msg, receivedTime, tempDir);

                Assert.NotNull(filePath);
                Assert.True(File.Exists(filePath));
                var content = File.ReadAllText(filePath);
                Assert.Contains("test/topic", content);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ExportToFile_WithSpecialCharactersInTopic_ShouldSanitizeFilename()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic("test/topic:with*special<chars>")
                    .WithPayload("test")
                    .Build();
                var receivedTime = DateTime.UtcNow;

                var filePath = _exporter.ExportToFile(msg, receivedTime, tempDir);

                Assert.NotNull(filePath);
                Assert.DoesNotContain(":", Path.GetFileName(filePath));
                Assert.DoesNotContain("*", Path.GetFileName(filePath));
                Assert.DoesNotContain("<", Path.GetFileName(filePath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ExportToFile_CreatesDirectoryIfNotExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic("test/topic")
                    .WithPayload("test")
                    .Build();
                var receivedTime = DateTime.UtcNow;

                var filePath = _exporter.ExportToFile(msg, receivedTime, tempDir);

                Assert.True(Directory.Exists(tempDir));
                Assert.NotNull(filePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ExportToFile_WithComplexMessage_ShouldExportAllProperties()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic("test/topic")
                    .WithPayload("payload")
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag()
                    .WithResponseTopic("response/topic")
                    .WithContentType("text/plain")
                    .WithUserProperty("key", Encoding.UTF8.GetBytes("value"))
                    .Build();
                var receivedTime = DateTime.UtcNow;

                var filePath = _exporter.ExportToFile(msg, receivedTime, tempDir);

                Assert.NotNull(filePath);
                var content = File.ReadAllText(filePath);
                Assert.Contains("\"QualityOfServiceLevel\": 2", content);
                Assert.Contains("response/topic", content);
                Assert.Contains("text/plain", content);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        #region ExportToFile error handling

        [Fact]
        public void ExportToFile_WithInvalidPath_ReturnsNull()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithPayload("test payload")
                .Build();
            var receivedTime = DateTime.UtcNow;

            // Use a path with characters invalid on Windows
            var invalidPath = Path.Combine("Z:\\nonexistent\\deeply\\nested\\path", Guid.NewGuid().ToString());

            var result = _exporter.ExportToFile(msg, receivedTime, invalidPath);

            Assert.Null(result);
        }

        #endregion

        #region ExportAllToFile validation

        [Fact]
        public void ExportAllToFile_WithNullMessages_ThrowsArgumentNullException()
        {
            var timestamps = new List<DateTime> { DateTime.UtcNow };

            Assert.Throws<ArgumentNullException>(() =>
                _exporter.ExportAllToFile(null!, timestamps, "output.json"));
        }

        [Fact]
        public void ExportAllToFile_WithNullTimestamps_ThrowsArgumentNullException()
        {
            var messages = new List<MqttApplicationMessage>
            {
                new MqttApplicationMessageBuilder().WithTopic("t").WithPayload("p").Build()
            };

            Assert.Throws<ArgumentNullException>(() =>
                _exporter.ExportAllToFile(messages, null!, "output.json"));
        }

        [Fact]
        public void ExportAllToFile_WithMismatchedCounts_ThrowsArgumentException()
        {
            var messages = new List<MqttApplicationMessage>
            {
                new MqttApplicationMessageBuilder().WithTopic("t").WithPayload("p").Build(),
                new MqttApplicationMessageBuilder().WithTopic("t2").WithPayload("p2").Build()
            };
            var timestamps = new List<DateTime> { DateTime.UtcNow };

            Assert.Throws<ArgumentException>(() =>
                _exporter.ExportAllToFile(messages, timestamps, "output.json"));
        }

        [Fact]
        public void ExportAllToFile_WithEmptyCollections_ReturnsNull()
        {
            var messages = new List<MqttApplicationMessage>();
            var timestamps = new List<DateTime>();

            var result = _exporter.ExportAllToFile(messages, timestamps, "output.json");

            Assert.Null(result);
        }

        #endregion

        #region ExportAllToFile file I/O

        [Fact]
        public void ExportAllToFile_WithValidMessages_CreatesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var messages = new List<MqttApplicationMessage>
                {
                    new MqttApplicationMessageBuilder().WithTopic("test/a").WithPayload("payload1").Build(),
                    new MqttApplicationMessageBuilder().WithTopic("test/b").WithPayload("payload2").Build()
                };
                var timestamps = new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow };
                var outputPath = Path.Combine(tempDir, "export.json");

                var result = _exporter.ExportAllToFile(messages, timestamps, outputPath);

                Assert.NotNull(result);
                Assert.Equal(outputPath, result);
                Assert.True(File.Exists(outputPath));
                var content = File.ReadAllText(outputPath);
                Assert.Contains("test/a", content);
                Assert.Contains("test/b", content);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ExportAllToFile_WithInvalidPath_ReturnsNull()
        {
            var messages = new List<MqttApplicationMessage>
            {
                new MqttApplicationMessageBuilder().WithTopic("t").WithPayload("p").Build()
            };
            var timestamps = new List<DateTime> { DateTime.UtcNow };
            var invalidPath = Path.Combine("Z:\\nonexistent\\deeply\\nested", "export.json");

            var result = _exporter.ExportAllToFile(messages, timestamps, invalidPath);

            Assert.Null(result);
        }

        #endregion
    }
}
