using Avalonia; // Added for VisualTreeAttachmentEventArgs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity; // Added for RoutedEventArgs
using Avalonia.ReactiveUI; // If using ReactiveUI bindings in code-behind
using Avalonia.Threading; // Added for Dispatcher
using CrowsNestMqtt.UI.ViewModels; // Namespace for MainViewModel

using ReactiveUI;

using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.ComponentModel;
using System.Reactive.Linq; // Added for INotifyPropertyChanged (optional but good practice)
using System; // Added for IDisposable
namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// Code-behind for the MainView.axaml user control.
/// </summary>
public partial class MainView : UserControl
{
    private INotifyCollectionChanged? _observableHistory;
    private IDisposable? _focusCommandSubscription; // Added for focus command

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
        // Delay focus slightly to ensure the control is fully ready
        Avalonia.Threading.Dispatcher.UIThread.Post(() => CommandAutoCompleteBox?.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Called when the control is detached from the visual tree.
    /// Cleans up event subscriptions.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeFromViewModel(); // Call combined unsubscribe method
        this.DataContextChanged -= OnDataContextChanged; // Unsubscribe from DataContext changes
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeFromViewModel(); // Unsubscribe from the old context first

        if (DataContext is MainViewModel vm && vm.FilteredMessageHistory is INotifyCollectionChanged observable)
        {
            _observableHistory = observable;
            _observableHistory.CollectionChanged += FilteredMessageHistory_CollectionChanged;
            // Optional: Also listen for SelectedMessage changes if needed for other reasons
            // vm.PropertyChanged += ViewModel_PropertyChanged;

             var viewModel = DataContext as MainViewModel;

            viewModel?
                .WhenAnyValue(x => x.ClipboardText)
                .DistinctUntilChanged()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe (async clipboardText => {
                    if (!string.IsNullOrWhiteSpace(clipboardText))
                    {
                        var clipboard = TopLevel.GetTopLevel(Parent as Visual)?.Clipboard;
                        var dataObject = new DataObject();
                        dataObject.Set(DataFormats.Text, clipboardText);
                        await (clipboard?.SetDataObjectAsync(dataObject) ?? Task.CompletedTask);
                    }
                });

            // Subscribe to the FocusCommandBarCommand
            _focusCommandSubscription = viewModel?.FocusCommandBarCommand
                .ObserveOn(RxApp.MainThreadScheduler) // Ensure focus happens on UI thread
                .Subscribe(_ =>
                {
                    CommandAutoCompleteBox?.Focus(); // Focus the control
                    // Optionally select all text?
                    // CommandAutoCompleteBox?.SelectAll();
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
