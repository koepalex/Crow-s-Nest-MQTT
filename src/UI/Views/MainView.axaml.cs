using Avalonia; // Added for VisualTreeAttachmentEventArgs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity; // Added for RoutedEventArgs
using Avalonia.Threading; // Added for Dispatcher
using CrowsNestMqtt.UI.ViewModels; // Namespace for MainViewModel

using ReactiveUI;

using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.Reactive.Linq; // Added for INotifyPropertyChanged (optional but good practice)
using System.Reactive; // Added for Unit
using AvaloniaEdit.Editing; // Added for Selection
using System.Diagnostics.CodeAnalysis;

// Dynamically load System.Windows.Forms for clipboard access on Windows
namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// Code-behind for the MainView.axaml user control.
/// </summary>
[ExcludeFromCodeCoverage] // UI event wiring and Avalonia-specific code is not unit testable
public partial class MainView : UserControl
{
    private INotifyCollectionChanged? _observableHistory;
    private IDisposable? _focusCommandSubscription; // Added for focus command
   private IDisposable? _gotFocusSubscription; // Added for window focus tracking
   private IDisposable? _lostFocusSubscription; // Added for window focus tracking
   private IDisposable? _clipboardInteractionSubscription; // Added for clipboard interaction
   private IDisposable? _rawPayloadDocumentSubscription; // Added for tracking document changes
   private Window? _parentWindow; // Added reference to the parent window
   private readonly AvaloniaEdit.TextEditor? _rawPayloadEditor; // Reference to the editor (now readonly)

    /// <summary>
    /// Initializes a new instance of the <see cref="MainView"/> class.
    /// </summary>
    public MainView()
    {
        InitializeComponent();

        // DataContext is typically set by the framework when this view is used,
        // often in App.axaml.cs or MainWindow.xaml/cs.
        // If needed, you could assign a design-time context here, but it's
        // usually handled in the XAML <Design.DataContext>

        // Example of accessing ViewModel if needed (ensure DataContext is set)
        // this.WhenActivated(disposables => {
        //     if (ViewModel is MainViewModel vm)
        //     {
        //         // Start timer when view is activated
        //         vm.StartTimer();
        //         // Stop timer when view is deactivated
        //         Disposable.Create(vm.StopTimer).DisposeWith(disposables);
        //     }
        // });

        // Find the editor control after initialization happens in InitializeComponent()
        _rawPayloadEditor = this.FindControl<AvaloniaEdit.TextEditor>("RawPayloadEditor");
        if (_rawPayloadEditor == null)
        {
             CrowsNestMqtt.Utils.AppLogger.Error("Could not find RawPayloadEditor control in MainView constructor.");
             // Consider throwing an exception or handling this more robustly if the editor is critical
        }

        // Subscribe to DataContext changes to hook/unhook event handlers
        this.DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Called when the control is attached to the visual tree.
    /// Sets the initial focus to the CommandTextBox.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // --- Initial Focus & Window Focus Tracking ---
        // Ensure DataContext is set and is the correct type
        if (DataContext is MainViewModel viewModel)
        {
            // Delay initial focus slightly to ensure the control is fully ready
            Dispatcher.UIThread.Post(() => CommandAutoCompleteBox?.Focus(), DispatcherPriority.Loaded);

            // Get the top-level control (usually the Window) - more reliable here
            _parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (_parentWindow != null)
            {
                // Set initial focus state
                viewModel.IsWindowFocused = _parentWindow.IsFocused;
                CrowsNestMqtt.Utils.AppLogger.Debug("MainView Attached. Initial IsWindowFocused = {IsFocused}", viewModel.IsWindowFocused);

                // Subscribe to focus events (unsubscribe handled in OnDetached/UnsubscribeFromViewModel)
#pragma warning disable IL2026 // Suppress trim warning for FromEventPattern
                _gotFocusSubscription = Observable.FromEventPattern<GotFocusEventArgs>(_parentWindow, nameof(Window.GotFocus))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        CrowsNestMqtt.Utils.AppLogger.Trace("MainView GotFocus event fired. Setting IsWindowFocused = true.");
                        viewModel.IsWindowFocused = true;
                    });
#pragma warning restore IL2026

#pragma warning disable IL2026 // Suppress trim warning for FromEventPattern
                _lostFocusSubscription = Observable.FromEventPattern<RoutedEventArgs>(_parentWindow, nameof(Window.LostFocus))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        CrowsNestMqtt.Utils.AppLogger.Trace("MainView LostFocus event fired. Setting IsWindowFocused = false.");
                        viewModel.IsWindowFocused = false;
                    });
#pragma warning restore IL2026
            }
            else
            {
                CrowsNestMqtt.Utils.AppLogger.Warning("Could not find parent window in MainView.OnAttachedToVisualTree to track focus.");
            }
        }
        else
        {
             CrowsNestMqtt.Utils.AppLogger.Warning("DataContext not ready or not MainViewModel in MainView.OnAttachedToVisualTree.");
        }
        // --- End Initial Focus & Window Focus Tracking ---
    }

    /// <summary>
    /// Called when the control is detached from the visual tree.
    /// Cleans up event subscriptions.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Focus subscriptions are cleaned up in UnsubscribeFromViewModel which is called below
        UnsubscribeFromViewModel(); // Call combined unsubscribe method
        this.DataContextChanged -= OnDataContextChanged; // Unsubscribe from DataContext changes
        base.OnDetachedFromVisualTree(e); // Call base method last
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeFromViewModel(); // Unsubscribe from the old context first

        if (DataContext is MainViewModel vm) // Check if DataContext is the correct type
        {
            // Subscribe to FilteredMessageHistory collection changes if it exists
            if (vm.FilteredMessageHistory is INotifyCollectionChanged observable)
            {
                _observableHistory = observable;
                _observableHistory.CollectionChanged += FilteredMessageHistory_CollectionChanged;
            }

            // Subscribe to ClipboardText changes
            vm.WhenAnyValue(x => x.ClipboardText)
              .DistinctUntilChanged()
              .ObserveOn(RxApp.MainThreadScheduler)
              .Subscribe(async clipboardText =>
              {
                  if (!string.IsNullOrWhiteSpace(clipboardText))
                  {
                      var clipboard = TopLevel.GetTopLevel(this)?.Clipboard; // Use 'this' directly
                      if (clipboard != null)
                      {
                          var dataObject = new DataObject();
                          dataObject.Set(DataFormats.Text, clipboardText);
                          await clipboard.SetDataObjectAsync(dataObject);
                      }
                  }
              }); // Note: Consider adding DisposeWith for this subscription if view model can change

            // Subscribe to the FocusCommandBarCommand
            _focusCommandSubscription = vm.FocusCommandBarCommand
                .ObserveOn(RxApp.MainThreadScheduler) // Ensure focus happens on UI thread
                .Subscribe(_ =>
               {
                   CommandAutoCompleteBox?.Focus(); // Focus the control
               });

            // Subscribe to the CopyTextToClipboardInteraction
            _clipboardInteractionSubscription = vm.CopyTextToClipboardInteraction.RegisterHandler(async interaction =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var clipboard = topLevel?.Clipboard;

                var textToCopy = interaction.Input;
                if (clipboard != null && textToCopy != null)
                {
                    try
                    {
                        await clipboard.SetTextAsync(textToCopy);
                        interaction.SetOutput(Unit.Default); // Signal completion
                        CrowsNestMqtt.Utils.AppLogger.Debug("Successfully copied text to clipboard via interaction.");
                    }
                    catch (Exception clipEx)
                    {
                        CrowsNestMqtt.Utils.AppLogger.Error(clipEx, "Failed to set clipboard text via interaction.");
                        // Optionally signal failure: interaction.SetException(clipEx);
                    }
                }
                else
                {
                    CrowsNestMqtt.Utils.AppLogger.Warning("Clipboard service not available in MainView interaction handler.");
                    // Optionally signal failure: interaction.SetException(new Exception("Clipboard not available."));
                }
            });

            // Subscribe to the CopyImageToClipboardInteraction
            vm.CopyImageToClipboardInteraction.RegisterHandler(async interaction =>
            {
                var bitmap = interaction.Input;
                if (bitmap == null)
                {
                    CrowsNestMqtt.Utils.AppLogger.Warning("No bitmap provided to CopyImageToClipboardInteraction.");
                    return;
                }

                try
                {
                    // Write image to temp file (PNG)
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crowsnest_image_{Guid.NewGuid():N}.png");
                    using (var fs = System.IO.File.OpenWrite(tempPath))
                    {
                        bitmap.Save(fs);
                    }

                    // Put the path into the clipboard as text
                    var topLevel = TopLevel.GetTopLevel(this);
                    var clipboard = topLevel?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(tempPath);
                        interaction.SetOutput(Unit.Default);
                        CrowsNestMqtt.Utils.AppLogger.Debug($"Image written to temp file and path copied to clipboard: {tempPath}");
                    }
                    else
                    {
                        CrowsNestMqtt.Utils.AppLogger.Warning("Clipboard service not available in MainView image interaction handler.");
                    }
                }
                catch (Exception ex)
                {
                    CrowsNestMqtt.Utils.AppLogger.Error(ex, "Failed to write image to temp file and set clipboard path.");
                }
            });

           // Focus tracking is now handled in OnAttachedToVisualTree

           // Subscribe to RawPayloadDocument changes to clear selection when empty
           _rawPayloadDocumentSubscription = vm.WhenAnyValue(x => x.RawPayloadDocument)
               .ObserveOn(RxApp.MainThreadScheduler) // Ensure UI access is on the correct thread
               .Subscribe(doc =>
               {
                   // Check if the editor and its TextArea are available
                   if (_rawPayloadEditor?.TextArea != null && (doc == null || doc.TextLength == 0))
                   {
                       // Clear selection by creating an empty selection at the start
                       _rawPayloadEditor.TextArea.Selection = Selection.Create(_rawPayloadEditor.TextArea, 0, 0);
                       CrowsNestMqtt.Utils.AppLogger.Debug("RawPayloadDocument changed and is empty. Cleared editor selection.");
                   }
               });
       }
    }

    // Renamed and expanded to handle all ViewModel subscriptions
    private void UnsubscribeFromViewModel()
    {
        // Unsubscribe from history collection changes
        if (_observableHistory != null)
        {
            _observableHistory.CollectionChanged -= FilteredMessageHistory_CollectionChanged;
            _observableHistory = null;
        }

        // Dispose focus command subscription
        _focusCommandSubscription?.Dispose();
        _focusCommandSubscription = null;
// Dispose focus event subscriptions
_gotFocusSubscription?.Dispose();
_gotFocusSubscription = null;
_lostFocusSubscription?.Dispose();
_lostFocusSubscription = null;
_clipboardInteractionSubscription?.Dispose(); // Dispose clipboard interaction subscription
_clipboardInteractionSubscription = null;
_rawPayloadDocumentSubscription?.Dispose(); // Dispose document subscription
_rawPayloadDocumentSubscription = null;
_parentWindow = null; // Clear window reference
// _rawPayloadEditor reference is cleared implicitly when view is destroyed
// Optional: Unsubscribe from PropertyChanged if you subscribed
// if (DataContext is MainViewModel oldVm)
// {
//     oldVm.PropertyChanged -= ViewModel_PropertyChanged;
// }
}

    private void FilteredMessageHistory_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Ensure we run this on the UI thread, especially if the collection
        // could be modified from a background thread (though DynamicData usually handles this).
        // Use Post to queue the action after the current layout pass might complete.
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainViewModel vm && vm.SelectedMessage != null && MessageHistoryListBox != null)
            {
                // Check if the selected item is still in the list after the change
                if (vm.FilteredMessageHistory.Contains(vm.SelectedMessage))
                {
                    MessageHistoryListBox.ScrollIntoView(vm.SelectedMessage);
                }
            }
        }, DispatcherPriority.Background); // Use Background priority to ensure layout is likely done
    }

    // Optional: Handle ViewModel property changes if needed
    // private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    // {
    //     if (e.PropertyName == nameof(MainViewModel.SelectedMessage))
    //     {
    //         // Potentially scroll into view when selection changes manually,
    //         // though ListBox usually handles this.
    //         // HandleSelectedMessageChange();
    //     }
    // }

    // InitializeComponent is automatically generated from the XAML.
    // No need to define it manually unless customizing the build process.
}
