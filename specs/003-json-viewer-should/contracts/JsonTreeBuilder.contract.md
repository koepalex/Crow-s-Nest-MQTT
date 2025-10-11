# Contract: JsonTreeBuilder

**Type**: Utility Class
**Layer**: Utils
**Responsibility**: Construct JsonTreeNode hierarchy from JSON with automatic expansion up to depth 5

## Public Interface

### Method: BuildTree

**Signature**:
```csharp
public JsonTreeNode BuildTree(JsonDocument document)
```

**Contract**:
- **Preconditions**:
  - `document` is not null
  - `document.RootElement` is valid JSON element

- **Postconditions**:
  - Returns non-null JsonTreeNode representing root
  - Root node has `Depth = 1`
  - All nodes at depth ≤ 5 have `IsExpanded = true` (if expandable)
  - All nodes at depth > 5 have `IsExpanded = false`
  - Tree structure matches JSON structure
  - Parent-child relationships correctly established

- **Exceptions**:
  - None (assumes valid JsonDocument)

**Test Cases**:
1. Flat JSON (depth 1) → Root node only, no children
2. 3-level nested object → All nodes expanded
3. 5-level nested object → All nodes expanded
4. 6-level nested object → Levels 1-5 expanded, level 6 collapsed
5. Mixed arrays and objects → Correct expansion per depth
6. Empty object/array → Expandable = false, no children

## Method: BuildTreeRecursive (Private Helper)

**Signature**:
```csharp
private JsonTreeNode BuildTreeRecursive(JsonElement element, string key, int depth, JsonTreeNode? parent)
```

**Contract**:
- **Preconditions**:
  - `element` is valid JsonElement
  - `key` is not null (may be empty string for root)
  - `depth` >= 1
  - `parent` is null only when depth == 1

- **Postconditions**:
  - Returns JsonTreeNode with correct properties populated
  - `node.Depth == depth`
  - `node.IsExpanded == (depth <= 5 && node.IsExpandable)`
  - Recursively processes children for Object/Array types

## Constants

```csharp
private const int MaxAutoExpandDepth = 5
```

## State Management

- **Stateless**: No instance fields, safe for concurrent use
- **Thread-Safety**: Read-only operations on input JsonDocument

## Dependencies

- System.Text.Json.JsonDocument
- System.Text.Json.JsonElement
- JsonTreeNode (data model)

## Performance Contract

- **Time Complexity**: O(n) where n = number of nodes up to depth 5
- **Space Complexity**: O(d) stack space where d = maximum depth traversed
- **Target**: < 100ms for typical payloads (<1000 nodes)

## Validation

Contract enforced by unit tests:
- `JsonTreeBuilderTests.BuildTree_FlatJson_ReturnsRootOnly()`
- `JsonTreeBuilderTests.BuildTree_5LevelNested_AllExpanded()`
- `JsonTreeBuilderTests.BuildTree_6LevelNested_OnlyFirst5Expanded()`
- `JsonTreeBuilderTests.BuildTree_MixedTypes_CorrectExpansion()`
