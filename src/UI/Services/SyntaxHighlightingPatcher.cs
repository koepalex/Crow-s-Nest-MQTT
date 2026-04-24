using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using Serilog;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Patches AvaloniaEdit's built-in syntax highlighting definitions so they
/// stay readable on the dark payload editor backgrounds used throughout the
/// app. The shipped Json.xshd hard-codes "Punctuation" foreground to
/// <c>Black</c>, which makes braces, brackets, colons and commas invisible
/// on a black background.
/// </summary>
public static class SyntaxHighlightingPatcher
{
    // Light gray — visible on the black payload editor background while still
    // letting the colored token rules (keys, strings, numbers) stand out.
    private static readonly Color PunctuationColor = Color.FromRgb(0xD4, 0xD4, 0xD4);

    private static bool _applied;

    /// <summary>
    /// Mutates the shared AvaloniaEdit highlighting definitions in place so
    /// every consumer of <see cref="HighlightingManager.Instance"/> sees the
    /// corrected colors. Idempotent.
    /// </summary>
    public static void ApplyDarkThemePatches()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            var json = HighlightingManager.Instance.GetDefinition("Json");
            var punct = json?.GetNamedColor("Punctuation");
            if (punct != null)
            {
                punct.Foreground = new SimpleHighlightingBrush(PunctuationColor);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to patch AvaloniaEdit syntax highlighting for dark theme.");
        }
    }

    /// <summary>
    /// Test-only reset so unit tests can re-apply and re-assert.
    /// </summary>
    internal static void ResetForTests() => _applied = false;
}
