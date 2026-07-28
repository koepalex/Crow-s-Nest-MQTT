using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CrowsNestMqtt.UI.Behaviors;

/// <summary>
/// Attached property that binds a <see cref="TextBlock"/>'s <see cref="TextBlock.Foreground"/> to a
/// theme resource identified by key.
/// <para>
/// Unlike an <c>IValueConverter</c> (which resolves a brush only once at bind time), this uses
/// <see cref="Avalonia.Controls.ResourceNodeExtensions.GetResourceObservable(Avalonia.Controls.IResourceHost, object, Func{object?, object?})"/>,
/// which re-emits whenever the resolved resource changes — including when the active
/// <see cref="Avalonia.Styling.ThemeVariant"/> is switched at runtime. This keeps the foreground
/// brush in sync with light/dark theme changes while the application is running.
/// </para>
/// </summary>
public static class DynamicForegroundResource
{
    /// <summary>The theme resource key whose brush should drive the foreground.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(DynamicForegroundResource));

    // Tracks the active resource subscription so it can be disposed/replaced when the key changes.
    private static readonly AttachedProperty<IDisposable?> SubscriptionProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("Subscription", typeof(DynamicForegroundResource));

    public static void SetKey(Control element, string? value) => element.SetValue(KeyProperty, value);

    public static string? GetKey(Control element) => element.GetValue(KeyProperty);

    static DynamicForegroundResource()
    {
        KeyProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            control.GetValue(SubscriptionProperty)?.Dispose();

            var key = args.GetNewValue<string?>();
            if (string.IsNullOrEmpty(key))
            {
                control.SetValue(SubscriptionProperty, null);
                return;
            }

            var subscription = control.Bind(
                TextBlock.ForegroundProperty,
                control.GetResourceObservable(key, static resource => resource is IBrush brush ? brush : null));

            control.SetValue(SubscriptionProperty, subscription);
        });
    }
}
