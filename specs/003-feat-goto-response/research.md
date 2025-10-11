# Research: Goto Response for MQTT V5 Request-Response

**Feature**: 003-feat-goto-response
**Phase**: 0 - Research & Technical Decisions
**Created**: 2025-09-29

## Research Questions & Findings

### 1. MQTT V5 Correlation-Data Implementation Patterns in .NET

**Decision**: Use MQTTnet library's built-in MQTT V5 support for correlation-data property handling

**Rationale**:
- MQTTnet provides native MQTT V5 support with direct access to message properties including correlation-data
- The `MqttApplicationMessage.UserProperties` and correlation-data are accessible via `MqttApplicationMessage.CorrelationData` property
- Existing codebase already uses MQTTnet, ensuring consistency and reducing dependency complexity

**Alternatives Considered**:
- Eclipse Paho .NET: Less mature MQTT V5 support, would require library migration
- Custom MQTT V5 implementation: Too complex, reinventing existing functionality
- HiveMQ .NET client: Commercial dependency, unnecessary complexity for this feature

**Implementation Pattern**:
```csharp
// Store correlation mappings in memory
private readonly ConcurrentDictionary<byte[], string> _correlationToResponseTopic = new();
private readonly ConcurrentDictionary<string, HashSet<byte[]>> _responseTopicToCorrelations = new();

// On request message with response-topic
if (message.ResponseTopic != null && message.CorrelationData != null)
{
    _correlationToResponseTopic[message.CorrelationData] = message.ResponseTopic;
    _responseTopicToCorrelations.GetOrAdd(message.ResponseTopic, _ => new()).Add(message.CorrelationData);
}
```

### 2. Real-Time UI Updates in WPF/Avalonia Applications

**Decision**: Use ReactiveUI with observable collections and async command patterns for real-time icon updates

**Rationale**:
- ReactiveUI provides excellent MVVM support for both WPF and Avalonia platforms
- Observable collections automatically update UI when message correlation states change
- Async commands prevent UI blocking during message processing
- Event-driven architecture aligns with MQTT message streaming requirements

**Alternatives Considered**:
- Direct property change notifications: More complex to manage, requires manual threading
- Timer-based polling: Inefficient, not truly real-time, adds unnecessary overhead
- Custom observable patterns: Reinventing ReactiveUI functionality

**Implementation Pattern**:
```csharp
public class MessageViewModel : ReactiveObject
{
    private ResponseStatus _responseStatus;
    public ResponseStatus ResponseStatus
    {
        get => _responseStatus;
        private set => this.RaiseAndSetIfChanged(ref _responseStatus, value);
    }

    public ReactiveCommand<Unit, Unit> NavigateToResponse { get; }
}
```

### 3. Icon State Management for Dynamic Message Status Indicators

**Decision**: Use SVG-based icon system with data binding to enum-based response status

**Rationale**:
- SVG icons scale properly across different DPI settings and platforms
- Avalonia has excellent SVG support, WPF can use vector graphics
- Enum-based status (Pending, Received, NavigationDisabled) provides clear state management
- Data templates allow automatic icon switching based on status

**Alternatives Considered**:
- Bitmap icons: Poor scalability, platform-specific rendering issues
- Font-based icons: Dependency on font files, potential loading issues
- Custom drawn icons: Performance overhead, complex to maintain

**Icon States**:
```csharp
public enum ResponseStatus
{
    Pending,          // Clock icon - response not yet received
    Received,         // Arrow icon - response available, clickable
    NavigationDisabled, // Disabled clock - response topic not subscribed
    Hidden            // No icon - no response-topic metadata
}
```

### 4. Message Correlation Persistence Strategy

**Decision**: In-memory correlation tracking with automatic cleanup based on configurable TTL

**Rationale**:
- Aligns with existing in-memory message buffer architecture
- Fast lookup performance for real-time correlation matching
- Automatic cleanup prevents memory leaks from orphaned correlations
- Configurable TTL allows tuning for different use case patterns

**Alternatives Considered**:
- Persistent storage: Overkill for temporary correlation data, adds complexity
- Unlimited in-memory storage: Memory leak risk with high-volume streams
- Session-based storage: Would lose correlations on restart unnecessarily

**Implementation Strategy**:
```csharp
public class MessageCorrelationService
{
    private readonly ConcurrentDictionary<CorrelationKey, CorrelationEntry> _correlations = new();
    private readonly Timer _cleanupTimer;

    private record CorrelationKey(byte[] Data) : IEquatable<CorrelationKey>;
    private record CorrelationEntry(string ResponseTopic, DateTime Created);
}
```

### 5. Cross-Platform Icon Rendering Approach

**Decision**: Use Avalonia's unified icon system with PathIcon for vector graphics

**Rationale**:
- PathIcon provides consistent rendering across Windows, Linux, and macOS
- Vector-based icons scale properly on high-DPI displays
- Single codebase for all platforms reduces maintenance burden
- Native theming support for light/dark mode compatibility

**Alternatives Considered**:
- Platform-specific icon implementations: Maintenance overhead, consistency issues
- External icon libraries: Additional dependencies, potential licensing concerns
- Bitmap fallbacks: Poor user experience on high-DPI displays

## Architecture Decisions Summary

1. **MQTT V5 Handling**: MQTTnet library with native correlation-data support
2. **UI Framework**: ReactiveUI for cross-platform MVVM with real-time updates
3. **Icon System**: SVG-based PathIcon with enum-driven state management
4. **Correlation Storage**: In-memory with TTL-based cleanup
5. **Performance**: Async/await throughout, non-blocking UI updates
6. **Testing**: Mockable service interfaces, integration tests with test brokers

## Risk Mitigation

- **High Message Volume**: Correlation cleanup and buffer limits prevent memory exhaustion
- **Platform Compatibility**: Avalonia PathIcon ensures consistent rendering across platforms
- **Response Topic Subscription**: Feature gracefully handles unsubscribed response topics
- **Correlation Collisions**: Byte array comparison with proper equality semantics

## Next Phase Dependencies

Phase 1 (Design & Contracts) can proceed with:
- Clear correlation-data handling patterns
- Established UI update mechanisms
- Defined icon state management approach
- Architectural boundaries between components