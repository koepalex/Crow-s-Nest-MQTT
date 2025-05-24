namespace Businesslogic.Configuration
{
    public record TopicBufferLimit
    {
        public string TopicFilter { get; init; }
        public long MaxSizeBytes { get; init; }
    }
}
