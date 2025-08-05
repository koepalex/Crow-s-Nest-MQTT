namespace CrowsNestMqtt.BusinessLogic.Configuration;

public record TopicBufferLimit(string TopicFilter, long MaxSizeBytes);
