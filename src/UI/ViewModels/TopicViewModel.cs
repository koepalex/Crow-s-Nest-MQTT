using ReactiveUI;

namespace CrowsNestMqtt.UI.ViewModels;

// Simple ViewModel for displaying a topic in the list
public class TopicViewModel : ReactiveObject
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private int _messageCount;
    public int MessageCount
    {
        get => _messageCount;
        set => this.RaiseAndSetIfChanged(ref _messageCount, value);
    }

    // Override ToString for simpler binding if needed, or use DisplayMemberPath
    public override string ToString() => Name;
}