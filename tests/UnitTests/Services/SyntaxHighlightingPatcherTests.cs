using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Highlighting;
using CrowsNestMqtt.UI.Services;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class SyntaxHighlightingPatcherTests
{
    [Fact]
    public void ApplyDarkThemePatches_ReplacesJsonPunctuationForeground()
    {
        // HighlightingManager is a process-wide singleton. Snapshot the
        // original so we restore it after the test to avoid leaking state
        // into other tests.
        var jsonDef = HighlightingManager.Instance.GetDefinition("Json");
        Assert.NotNull(jsonDef);
        var punct = jsonDef!.GetNamedColor("Punctuation");
        Assert.NotNull(punct);
        var originalForeground = punct!.Foreground;

        try
        {
            SyntaxHighlightingPatcher.ResetForTests();
            SyntaxHighlightingPatcher.ApplyDarkThemePatches();

            var patchedBrush = punct.Foreground as SimpleHighlightingBrush;
            Assert.NotNull(patchedBrush);

            // The patched foreground must no longer be black — that's the whole
            // point of the patch on the dark background used by the editors.
            var brushObj = patchedBrush!.GetBrush(null);
            var solid = Assert.IsType<ImmutableSolidColorBrush>(brushObj);
            Assert.NotEqual(Colors.Black, solid.Color);
        }
        finally
        {
            // Restore original so the rest of the test suite doesn't see a
            // mutated shared definition.
            punct.Foreground = originalForeground;
            SyntaxHighlightingPatcher.ResetForTests();
        }
    }

    [Fact]
    public void ApplyDarkThemePatches_IsIdempotent()
    {
        var jsonDef = HighlightingManager.Instance.GetDefinition("Json");
        var punct = jsonDef!.GetNamedColor("Punctuation");
        var originalForeground = punct!.Foreground;

        try
        {
            SyntaxHighlightingPatcher.ResetForTests();
            SyntaxHighlightingPatcher.ApplyDarkThemePatches();
            var first = punct.Foreground;

            // Second call must be a no-op (not overwrite a custom Foreground
            // that a later test might inject).
            SyntaxHighlightingPatcher.ApplyDarkThemePatches();
            Assert.Same(first, punct.Foreground);
        }
        finally
        {
            punct.Foreground = originalForeground;
            SyntaxHighlightingPatcher.ResetForTests();
        }
    }
}
