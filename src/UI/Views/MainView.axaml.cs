using Avalonia; // Added for VisualTreeAttachmentEventArgs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform; // Added for IClipboard
using Avalonia.Interactivity; // Added for RoutedEventArgs
using Avalonia.ReactiveUI; // If using ReactiveUI bindings in code-behind
using Avalonia.Threading; // Added for Dispatcher
using CrowsNestMqtt.UI.ViewModels; // Namespace for MainViewModel

using ReactiveUI;

using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.ComponentModel;
using System.Reactive.Linq; // Added for INotifyPropertyChanged (optional but good practice)
using System.Reactive; // Added for Unit
using System; // Added for IDisposable
namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// Code-behind for the MainView.axaml user control.
/// </summary>
public partial class MainView : UserControl
{
    private INotifyCollectionChanged? _observableHistory;
    private IDisposable? _focusCommandSubscription; // Added for focus command
   private IDisposable? _gotFocusSubscription; // Added for window focus tracking
   private IDisposable? _lostFocusSubscription; // Added for window focus tracking
   private IDisposable? _clipboardInteractionSubscription; // Added for clipboard interaction
   private Window? _parentWindow; // Added reference to the parent window

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
                Serilog.Log.Debug("MainView Attached. Initial IsWindowFocused = {IsFocused}", viewModel.IsWindowFocused);

                // Subscribe to focus events (unsubscribe handled in OnDetached/UnsubscribeFromViewModel)
                _gotFocusSubscription = Observable.FromEventPattern<GotFocusEventArgs>(_parentWindow, nameof(Window.GotFocus))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        Serilog.Log.Debug("MainView GotFocus event fired. Setting IsWindowFocused = true.");
                        viewModel.IsWindowFocused = true;
                    });

                _lostFocusSubscription = Observable.FromEventPattern<RoutedEventArgs>(_parentWindow, nameof(Window.LostFocus))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        Serilog.Log.Debug("MainView LostFocus event fired. Setting IsWindowFocused = false.");
                        viewModel.IsWindowFocused = false;
                    });
            }
            else
            {
                Serilog.Log.Warning("Could not find parent window in MainView.OnAttachedToVisualTree to track focus.");
            }
        }
        else
        {
             Serilog.Log.Warning("DataContext not ready or not MainViewModel in MainView.OnAttachedToVisualTree.");
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
               var textToCopy = interaction.Input;
               var topLevel = TopLevel.GetTopLevel(this);
               var clipboard = topLevel?.Clipboard;

               if (clipboard != null && textToCopy != null)
               {
                   try
                   {
                       await clipboard.SetTextAsync(textToCopy);
                       interaction.SetOutput(Unit.Default); // Signal completion
                       Serilog.Log.Debug("Successfully copied text to clipboard via interaction.");
                   }
                   catch (Exception clipEx)
                   {
                       Serilog.Log.Error(clipEx, "Failed to set clipboard text via interaction.");
                       // Optionally signal failure: interaction.SetException(clipEx);
                   }
               }
               else
               {
                   Serilog.Log.Warning("Clipboard service not available in MainView interaction handler.");
                   // Optionally signal failure: interaction.SetException(new Exception("Clipboard not available."));
               }
           });

           // Focus tracking is now handled in OnAttachedToVisualTree
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
_parentWindow = null; // Clear window reference

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
