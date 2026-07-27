using System.Text.Json;

namespace CrowsNestMqtt.UI.Theming;

/// <summary>
/// Maps a <see cref="JsonValueKind"/> to a FluentAvalonia semantic brush resource key.
/// <para>
/// Instead of hard-coded colors (which only read well on a dark background), the JSON viewer
/// resolves these FluentAvalonia (WinUI) resource keys against the active theme so the syntax
/// highlighting stays legible in both light and dark mode:
/// </para>
/// <list type="bullet">
///   <item><description>String / <c>true</c> → success brush</description></item>
///   <item><description>Number → attention (information) brush</description></item>
///   <item><description><c>false</c> → critical (danger) brush</description></item>
///   <item><description><c>null</c> → secondary font brush</description></item>
///   <item><description>Objects, arrays and any fallback → primary font brush</description></item>
/// </list>
/// The keys are looked up reactively in the view (see <c>DynamicForegroundResource</c>) so the
/// colors also update when the theme is switched at runtime.
/// </summary>
public static class JsonValueBrushResources
{
    // FluentAvalonia (WinUI) semantic brush resource keys. These are defined per ThemeVariant.
    public const string SuccessBrushKey = "SystemFillColorSuccessBrush";
    public const string AttentionBrushKey = "SystemFillColorAttentionBrush";
    public const string CriticalBrushKey = "SystemFillColorCriticalBrush";
    public const string TextPrimaryBrushKey = "TextFillColorPrimaryBrush";
    public const string TextSecondaryBrushKey = "TextFillColorSecondaryBrush";

    /// <summary>
    /// Returns the FluentAvalonia resource key used to highlight a value of the given kind.
    /// </summary>
    public static string GetResourceKey(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => SuccessBrushKey,
        JsonValueKind.Number => AttentionBrushKey,
        JsonValueKind.True => SuccessBrushKey,
        JsonValueKind.False => CriticalBrushKey,
        JsonValueKind.Null => TextSecondaryBrushKey,
        _ => TextPrimaryBrushKey
    };
}
