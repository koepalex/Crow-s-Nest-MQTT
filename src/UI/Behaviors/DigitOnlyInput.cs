using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;

namespace CrowsNestMqtt.UI.Behaviors;

/// <summary>
/// Attached behavior that restricts a <see cref="TextBox"/> to digit-only input.
/// <para>
/// Any non-digit characters are stripped as soon as they appear (covering typing, pasting,
/// drag-drop and IME input), and the caret position is preserved. When the text is cleared,
/// the box falls back to <c>"0"</c> unless <see cref="AllowEmptyProperty"/> is set to
/// <see langword="true"/> (e.g. for fields where empty carries a meaning such as "infinite").
/// </para>
/// </summary>
public static class DigitOnlyInput
{
    /// <summary>Enables digit-only filtering on the attached <see cref="TextBox"/>.</summary>
    public static readonly AttachedProperty<bool> EnableProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Enable", typeof(DigitOnlyInput));

    /// <summary>When true, clearing the text leaves it empty instead of falling back to "0".</summary>
    public static readonly AttachedProperty<bool> AllowEmptyProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("AllowEmpty", typeof(DigitOnlyInput));

    /// <summary>Optional inclusive minimum value the entered number is clamped to.</summary>
    public static readonly AttachedProperty<long?> MinValueProperty =
        AvaloniaProperty.RegisterAttached<TextBox, long?>("MinValue", typeof(DigitOnlyInput));

    /// <summary>Optional inclusive maximum value the entered number is clamped to.</summary>
    public static readonly AttachedProperty<long?> MaxValueProperty =
        AvaloniaProperty.RegisterAttached<TextBox, long?>("MaxValue", typeof(DigitOnlyInput));

    public static void SetEnable(TextBox element, bool value) => element.SetValue(EnableProperty, value);

    public static bool GetEnable(TextBox element) => element.GetValue(EnableProperty);

    public static void SetAllowEmpty(TextBox element, bool value) => element.SetValue(AllowEmptyProperty, value);

    public static bool GetAllowEmpty(TextBox element) => element.GetValue(AllowEmptyProperty);

    public static void SetMinValue(TextBox element, long? value) => element.SetValue(MinValueProperty, value);

    public static long? GetMinValue(TextBox element) => element.GetValue(MinValueProperty);

    public static void SetMaxValue(TextBox element, long? value) => element.SetValue(MaxValueProperty, value);

    public static long? GetMaxValue(TextBox element) => element.GetValue(MaxValueProperty);

    static DigitOnlyInput()
    {
        EnableProperty.Changed.AddClassHandler<TextBox>((textBox, args) =>
        {
            if (args.GetNewValue<bool>())
            {
                textBox.TextChanged += OnTextChanged;
            }
            else
            {
                textBox.TextChanged -= OnTextChanged;
            }
        });
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (string.IsNullOrEmpty(textBox.Text))
        {
            if (!GetAllowEmpty(textBox))
            {
                textBox.Text = "0";
                textBox.CaretIndex = 1;
            }
            return;
        }

        var digitsOnly = new string(textBox.Text.Where(char.IsDigit).ToArray());
        if (digitsOnly != textBox.Text)
        {
            var caret = textBox.CaretIndex;
            var removed = textBox.Text.Length - digitsOnly.Length;
            textBox.Text = digitsOnly;
            textBox.CaretIndex = Math.Max(0, caret - removed);
            return;
        }

        // Clamp to the configured [MinValue, MaxValue] range, if any.
        var min = GetMinValue(textBox);
        var max = GetMaxValue(textBox);
        if ((min.HasValue || max.HasValue) && long.TryParse(digitsOnly, out var value))
        {
            var clamped = value;
            if (max.HasValue && clamped > max.Value) clamped = max.Value;
            if (min.HasValue && clamped < min.Value) clamped = min.Value;

            if (clamped != value)
            {
                var clampedText = clamped.ToString(System.Globalization.CultureInfo.InvariantCulture);
                textBox.Text = clampedText;
                textBox.CaretIndex = clampedText.Length;
            }
        }
    }
}
