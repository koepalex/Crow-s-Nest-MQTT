namespace CrowsNestMqtt.UI.ViewModels;

using CrowsNestMqtt.BusinessLogic.Configuration;
using ReactiveUI;

public class AutoLogTopicRuleViewModel : ReactiveObject
{
    private string _topicFilter = "new/topic/filter";
    private bool _isEnabled = true;

    public AutoLogTopicRuleViewModel()
    {
    }

    public AutoLogTopicRuleViewModel(AutoLogTopicRule rule)
    {
        _topicFilter = rule.TopicFilter;
        _isEnabled = rule.IsEnabled;
    }

    public string TopicFilter
    {
        get => _topicFilter;
        set => this.RaiseAndSetIfChanged(ref _topicFilter, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }
}
