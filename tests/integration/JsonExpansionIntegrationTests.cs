using System.Text.Json;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests
{
    /// <summary>
    /// Integration tests for JSON viewer expansion behavior across application contexts.
    /// These tests verify acceptance scenarios from spec.md (003-json-viewer-should).
    /// </summary>
    public class JsonExpansionIntegrationTests
    {
        // AC-6: Up to 5 levels auto-expand
        [Fact]
        public void FiveLevelJson_FullyExpanded()
        {
            // This test will FAIL until T007 (JsonTreeBuilder) is complete

            // Arrange
            var json = "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"value\":\"level 5\"}}}}}}";
            var document = JsonDocument.Parse(json);

            // Act
            var root = CrowsNestMqtt.Utils.JsonTreeBuilder.BuildTree(document);

            // Assert
            // Traverse to depth 5 and verify all expanded
            var current = root;
            for (int expectedDepth = 1; expectedDepth <= 5; expectedDepth++)
            {
                Assert.Equal(expectedDepth, current.Depth);
                Assert.True(current.IsExpanded, $"Depth {expectedDepth} should be expanded");

                if (expectedDepth < 5)
                {
                    Assert.NotEmpty(current.Children);
                    current = current.Children[0];
                }
            }

            // Verify value "level 5" is visible (within expanded node)
            Assert.NotEmpty(current.Children);
        }

        // AC-7: Beyond 5 levels remain collapsed
        [Fact]
        public void SixPlusLevelJson_PartiallyExpanded()
        {
            // This test will FAIL until T007 (JsonTreeBuilder) is complete

            // Arrange
            var json = "{\"l1\":{\"l2\":{\"l3\":{\"l4\":{\"l5\":{\"l6\":{\"l7\":{\"value\":\"too deep\"}}}}}}}}";
            var document = JsonDocument.Parse(json);

            // Act
            var root = CrowsNestMqtt.Utils.JsonTreeBuilder.BuildTree(document);

            // Assert
            // Levels 1-5 expanded
            var current = root;
            for (int expectedDepth = 1; expectedDepth <= 5; expectedDepth++)
            {
                Assert.Equal(expectedDepth, current.Depth);
                Assert.True(current.IsExpanded, $"Depth {expectedDepth} should be expanded");
                Assert.NotEmpty(current.Children);
                current = current.Children[0];
            }

            // Level 6 collapsed
            Assert.Equal(6, current.Depth);
            Assert.False(current.IsExpanded, "Depth 6 should be collapsed");

            // Level 7 exists but collapsed
            Assert.NotEmpty(current.Children);
            var level7 = current.Children[0];
            Assert.Equal(7, level7.Depth);
            Assert.False(level7.IsExpanded, "Depth 7 should be collapsed");
        }

        // Edge Case: Large JSON (1000+ properties) renders without UI blocking
        [Fact]
        public void LargeJson_RendersWithoutBlocking()
        {
            // This test will FAIL until T013 (performance tests) establishes baseline

            // Arrange - Create large JSON with 1000 properties
            var properties = new System.Collections.Generic.Dictionary<string, object>();
            for (int i = 0; i < 1000; i++)
            {
                properties[$"prop{i}"] = $"value{i}";
            }
            var json = System.Text.Json.JsonSerializer.Serialize(properties);
            var document = JsonDocument.Parse(json);

            // Act - Measure tree construction time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var root = CrowsNestMqtt.Utils.JsonTreeBuilder.BuildTree(document);
            stopwatch.Stop();

            // Assert
            // Tree renders in <1 second (performance requirement)
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Tree construction took {stopwatch.ElapsedMilliseconds}ms, expected <1000ms");

            // All properties visible (expanded)
            Assert.Equal(1000, root.Children.Count);
        }

        // Edge Case: Deep nesting (10+ levels) handled correctly
        [Fact]
        public void DeepNesting_HandledCorrectly()
        {
            // This test will FAIL until T007 (JsonTreeBuilder) is complete

            // Arrange - 10-level deep nesting
            var json = "{\"l1\":{\"l2\":{\"l3\":{\"l4\":{\"l5\":{\"l6\":{\"l7\":{\"l8\":{\"l9\":{\"l10\":\"deepest\"}}}}}}}}}}";
            var document = JsonDocument.Parse(json);

            // Act
            var root = CrowsNestMqtt.Utils.JsonTreeBuilder.BuildTree(document);

            // Assert
            // First 5 levels expanded
            var current = root;
            for (int i = 1; i <= 5; i++)
            {
                Assert.True(current.IsExpanded, $"Level {i} should be expanded");
                current = current.Children[0];
            }

            // Levels 6-10 collapsed
            for (int i = 6; i <= 10; i++)
            {
                Assert.False(current.IsExpanded, $"Level {i} should be collapsed");
                if (i < 10)
                {
                    Assert.NotEmpty(current.Children);
                    current = current.Children[0];
                }
            }
        }

        // Edge Case: Malformed JSON shows error message
        [Fact]
        public void MalformedJson_ShowsErrorMessage()
        {
            // This test should already pass with current JsonViewerViewModel implementation

            // Arrange
            var vm = new CrowsNestMqtt.UI.ViewModels.JsonViewerViewModel();
            var malformedJson = "{\"broken\":\"json";

            // Act
            vm.LoadJson(malformedJson);

            // Assert
            Assert.True(vm.HasParseError);
            Assert.Contains("JSON Parsing Error", vm.JsonParseError);
            Assert.Empty(vm.RootNodes);
        }
    }
}
