using ReactiveUI;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UI.ViewModels
{
    public class TopicBufferLimitViewModel : ReactiveObject
    {
        private string _topicFilter = "";
        public string TopicFilter
        {
            get => _topicFilter;
            set => this.RaiseAndSetIfChanged(ref _topicFilter, value);
        }

        private long _maxSizeBytes;
        public long MaxSizeBytes
        {
            get => _maxSizeBytes;
            set => this.RaiseAndSetIfChanged(ref _maxSizeBytes, value);
        }

        public TopicBufferLimitViewModel()
        {
        }

        public TopicBufferLimitViewModel(TopicBufferLimit model)
        {
            TopicFilter = model.TopicFilter;
            MaxSizeBytes = model.MaxSizeBytes;
        }
    }
}
