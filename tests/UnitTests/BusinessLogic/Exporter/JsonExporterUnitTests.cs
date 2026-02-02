using System;
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
                .WithUserProperty("key1", "value1")
                .WithUserProperty("key2", "value2")
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
                    .WithUserProperty("key", "value")
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
    }
}
