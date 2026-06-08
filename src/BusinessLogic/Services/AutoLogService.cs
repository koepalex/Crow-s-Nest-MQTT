namespace CrowsNestMqtt.BusinessLogic.Services;

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrowsNestMqtt.BusinessLogic.Configuration;
using Microsoft.Data.Sqlite;
using MQTTnet;
using MQTTnet.Packets;
using Serilog;

public sealed class AutoLogService : IAutoLogService
{
    private const long DefaultMaxDatabaseSizeBytes = 100 * 1024 * 1024;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, HashSet<string>> _knownJsonColumnsByTable = new(StringComparer.OrdinalIgnoreCase);
    private List<AutoLogTopicRule> _rules = new();
    private string _databasePath = GetDefaultDatabasePath(null);
    private long _maxDatabaseSizeBytes = DefaultMaxDatabaseSizeBytes;
    private bool _disposed;

    public void UpdateConfiguration(string? exportPath, IEnumerable<AutoLogTopicRule> rules, long maxDatabaseSizeBytes)
    {
        _databasePath = GetDefaultDatabasePath(exportPath);
        _rules = rules
            .Where(r => !string.IsNullOrWhiteSpace(r.TopicFilter))
            .Select(r => new AutoLogTopicRule(NormalizeTopicFilter(r.TopicFilter), r.IsEnabled))
            .ToList();
        _maxDatabaseSizeBytes = maxDatabaseSizeBytes > 0 ? maxDatabaseSizeBytes : DefaultMaxDatabaseSizeBytes;
    }

    public bool IsEnabledForTopic(string topic)
    {
        return ResolveRule(topic) is { IsEnabled: true };
    }

    public async Task LogBatchAsync(IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs> batch, CancellationToken cancellationToken = default)
    {
        if (_disposed || batch.Count == 0 || _rules.Count == 0)
        {
            return;
        }

        var matched = batch
            .Select(item => (Item: item, Rule: ResolveRule(item.Topic)))
            .Where(item => item.Rule is { IsEnabled: true })
            .ToList();

        if (matched.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString());

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, "PRAGMA busy_timeout=5000;", cancellationToken).ConfigureAwait(false);
            await EnsureGlobalTablesAsync(connection, cancellationToken).ConfigureAwait(false);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            foreach (var (item, rule) in matched)
            {
                if (rule == null)
                {
                    continue;
                }

                await LogMessageAsync(connection, transaction, item, rule.TopicFilter, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await PruneIfNeededAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to auto-log MQTT batch to SQLite at {Path}", _databasePath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _writeLock.Dispose();
    }

    public static string GetTableNameForTopic(string topic)
    {
        var normalized = NormalizeTopicFilter(topic);
        var sanitized = Regex.Replace(normalized, "[^A-Za-z0-9_]+", "_").Trim('_').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "root";
        }

        if (sanitized.Length > 72)
        {
            sanitized = sanitized[..72].Trim('_');
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..8].ToLowerInvariant();
        return $"mqtt_{sanitized}_{hash}";
    }

    private async Task LogMessageAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, IdentifiedMqttApplicationMessageReceivedEventArgs item, string sourceFilter, CancellationToken cancellationToken)
    {
        var message = item.ApplicationMessage;
        var topic = item.Topic;
        var tableName = GetTableNameForTopic(topic);
        await EnsureTopicTableAsync(connection, transaction, tableName, topic, sourceFilter, cancellationToken).ConfigureAwait(false);

        var payload = message.Payload.ToArray();
        var payloadInfo = BuildPayloadInfo(message, payload);
        if (payloadInfo.JsonFields.Count > 0)
        {
            await EnsureJsonColumnsAsync(connection, transaction, tableName, payloadInfo.JsonFields, cancellationToken).ConfigureAwait(false);
        }

        var metadataJson = JsonSerializer.Serialize(BuildMetadata(item));
        var userPropertiesJson = JsonSerializer.Serialize(message.UserProperties?.ToDictionary(p => p.Name, p => p.ReadValueAsString()) ?? new Dictionary<string, string>());
        var receivedAtUtc = DateTime.UtcNow.ToString("O");

        var sql = $"""
            INSERT INTO {QuoteIdentifier(tableName)} (
                received_at_utc, message_id, topic, is_retained, is_own_message,
                qos, retain, payload_format_indicator, content_type, response_topic,
                correlation_data_hex, message_expiry_interval, metadata_json, user_properties_json,
                payload_size, payload_json, payload_text, payload_xml, payload_base64, payload_blob)
            VALUES (
                $received_at_utc, $message_id, $topic, $is_retained, $is_own_message,
                $qos, $retain, $payload_format_indicator, $content_type, $response_topic,
                $correlation_data_hex, $message_expiry_interval, $metadata_json, $user_properties_json,
                $payload_size, $payload_json, $payload_text, $payload_xml, $payload_base64, $payload_blob)
            RETURNING id;
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$received_at_utc", receivedAtUtc);
        command.Parameters.AddWithValue("$message_id", item.MessageId.ToString());
        command.Parameters.AddWithValue("$topic", topic);
        command.Parameters.AddWithValue("$is_retained", item.IsEffectivelyRetained ? 1 : 0);
        command.Parameters.AddWithValue("$is_own_message", item.IsOwnMessage ? 1 : 0);
        command.Parameters.AddWithValue("$qos", (int)message.QualityOfServiceLevel);
        command.Parameters.AddWithValue("$retain", message.Retain ? 1 : 0);
        command.Parameters.AddWithValue("$payload_format_indicator", (int)message.PayloadFormatIndicator);
        command.Parameters.AddWithValue("$content_type", (object?)message.ContentType ?? DBNull.Value);
        command.Parameters.AddWithValue("$response_topic", (object?)message.ResponseTopic ?? DBNull.Value);
        command.Parameters.AddWithValue("$correlation_data_hex", message.CorrelationData == null ? DBNull.Value : Convert.ToHexString(message.CorrelationData));
        command.Parameters.AddWithValue("$message_expiry_interval", (long)message.MessageExpiryInterval);
        command.Parameters.AddWithValue("$metadata_json", metadataJson);
        command.Parameters.AddWithValue("$user_properties_json", userPropertiesJson);
        command.Parameters.AddWithValue("$payload_size", payload.Length);
        command.Parameters.AddWithValue("$payload_json", (object?)payloadInfo.Json ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload_text", (object?)payloadInfo.Text ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload_xml", (object?)payloadInfo.Xml ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload_base64", (object?)payloadInfo.Base64 ?? DBNull.Value);
        command.Parameters.Add("$payload_blob", SqliteType.Blob).Value = (object?)payloadInfo.Blob ?? DBNull.Value;

        var rowId = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);

        await using var indexCommand = connection.CreateCommand();
        indexCommand.Transaction = (SqliteTransaction)transaction;
        indexCommand.CommandText = """
            INSERT INTO _auto_log_entries (table_name, row_id, received_at_utc, payload_size)
            VALUES ($table_name, $row_id, $received_at_utc, $payload_size);
            """;
        indexCommand.Parameters.AddWithValue("$table_name", tableName);
        indexCommand.Parameters.AddWithValue("$row_id", rowId);
        indexCommand.Parameters.AddWithValue("$received_at_utc", receivedAtUtc);
        indexCommand.Parameters.AddWithValue("$payload_size", payload.Length);
        await indexCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureGlobalTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS _auto_log_topics (
                topic TEXT PRIMARY KEY,
                table_name TEXT NOT NULL UNIQUE,
                source_filter TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                last_seen_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS _auto_log_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                table_name TEXT NOT NULL,
                row_id INTEGER NOT NULL,
                received_at_utc TEXT NOT NULL,
                payload_size INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_auto_log_entries_received_at ON _auto_log_entries(received_at_utc, id);
            """, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureTopicTableAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, string tableName, string topic, string sourceFilter, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                received_at_utc TEXT NOT NULL,
                message_id TEXT NOT NULL,
                topic TEXT NOT NULL,
                is_retained INTEGER NOT NULL,
                is_own_message INTEGER NOT NULL,
                qos INTEGER NOT NULL,
                retain INTEGER NOT NULL,
                payload_format_indicator INTEGER NOT NULL,
                content_type TEXT NULL,
                response_topic TEXT NULL,
                correlation_data_hex TEXT NULL,
                message_expiry_interval INTEGER NOT NULL,
                metadata_json TEXT NOT NULL,
                user_properties_json TEXT NOT NULL,
                payload_size INTEGER NOT NULL,
                payload_json TEXT NULL,
                payload_text TEXT NULL,
                payload_xml TEXT NULL,
                payload_base64 TEXT NULL,
                payload_blob BLOB NULL
            );
            CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"idx_{tableName}_received_at")} ON {QuoteIdentifier(tableName)}(received_at_utc);
            INSERT INTO _auto_log_topics (topic, table_name, source_filter, created_at_utc, last_seen_at_utc)
            VALUES ($topic, $table_name, $source_filter, $now, $now)
            ON CONFLICT(topic) DO UPDATE SET
                source_filter = excluded.source_filter,
                last_seen_at_utc = excluded.last_seen_at_utc;
            """;
        command.Parameters.AddWithValue("$topic", topic);
        command.Parameters.AddWithValue("$table_name", tableName);
        command.Parameters.AddWithValue("$source_filter", sourceFilter);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureJsonColumnsAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, string tableName, IReadOnlyCollection<string> fields, CancellationToken cancellationToken)
    {
        if (!_knownJsonColumnsByTable.TryGetValue(tableName, out var known))
        {
            known = await LoadColumnNamesAsync(connection, transaction, tableName, cancellationToken).ConfigureAwait(false);
            _knownJsonColumnsByTable[tableName] = known;
        }

        foreach (var field in fields)
        {
            var column = "json_" + SanitizeColumnName(field);
            if (!known.Add(column))
            {
                continue;
            }

            var jsonPath = BuildJsonPath(field);
            await ExecuteNonQueryAsync(connection, transaction, $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {QuoteIdentifier(column)} TEXT GENERATED ALWAYS AS (json_extract(payload_json, {QuoteLiteral(jsonPath)})) VIRTUAL;", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, transaction, $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"idx_{tableName}_{column}")} ON {QuoteIdentifier(tableName)}({QuoteIdentifier(column)});", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HashSet<string>> LoadColumnNamesAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, string tableName, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"PRAGMA table_xinfo({QuoteIdentifier(tableName)});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(1));
        }

        return result;
    }

    private async Task PruneIfNeededAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken).ConfigureAwait(false);
        if (!File.Exists(_databasePath) || new FileInfo(_databasePath).Length <= _maxDatabaseSizeBytes)
        {
            return;
        }

        while (new FileInfo(_databasePath).Length > _maxDatabaseSizeBytes)
        {
            await using var select = connection.CreateCommand();
            select.CommandText = "SELECT id, table_name, row_id FROM _auto_log_entries ORDER BY received_at_utc, id LIMIT 250;";
            var rows = new List<(long Id, string TableName, long RowId)>();
            await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2)));
                }
            }

            if (rows.Count == 0)
            {
                break;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                await ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {QuoteIdentifier(row.TableName)} WHERE id = {row.RowId};", cancellationToken).ConfigureAwait(false);
                await ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM _auto_log_entries WHERE id = {row.Id};", cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, "VACUUM;", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken).ConfigureAwait(false);
        }
    }

    private AutoLogTopicRule? ResolveRule(string topic)
    {
        AutoLogTopicRule? best = null;
        var bestScore = -1;
        foreach (var rule in _rules)
        {
            var score = MatchTopic(topic, rule.TopicFilter);
            if (score > bestScore)
            {
                best = rule;
                bestScore = score;
            }
        }

        return bestScore >= 0 ? best : null;
    }

    public static int MatchTopic(string topic, string filter)
    {
        if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(filter))
        {
            return -1;
        }

        if (filter == topic)
        {
            return 1000;
        }

        var topicSegments = topic.Split('/');
        var filterSegments = filter.Split('/');
        var score = 0;
        var i = 0;
        var j = 0;

        while (i < topicSegments.Length && j < filterSegments.Length)
        {
            if (filterSegments[j] == "#")
            {
                if (j == filterSegments.Length - 1)
                {
                    i = topicSegments.Length;
                    break;
                }

                return -1;
            }

            if (filterSegments[j] == topicSegments[i])
            {
                score += 10;
            }
            else if (filterSegments[j] == "+")
            {
                score += 5;
            }
            else
            {
                return -1;
            }

            i++;
            j++;
        }

        if (i == topicSegments.Length && j == filterSegments.Length)
        {
            return score;
        }

        if (j == filterSegments.Length - 1 && filterSegments[j] == "#" && i == topicSegments.Length)
        {
            return score + 1;
        }

        return -1;
    }

    private static PayloadInfo BuildPayloadInfo(MqttApplicationMessage message, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return new PayloadInfo(Text: string.Empty);
        }

        var contentType = message.ContentType ?? string.Empty;
        var decoded = TryDecodeUtf8(payload);
        var looksJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
        if ((looksJson || decoded != null) && TryParseJson(decoded, out var jsonFields))
        {
            return new PayloadInfo(Json: decoded, JsonFields: jsonFields);
        }

        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return decoded != null ? new PayloadInfo(Xml: decoded) : new PayloadInfo(Base64: Convert.ToBase64String(payload));
        }

        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || decoded != null && message.PayloadFormatIndicator.ToString().Contains("Character", StringComparison.OrdinalIgnoreCase))
        {
            return decoded != null ? new PayloadInfo(Text: decoded) : new PayloadInfo(Base64: Convert.ToBase64String(payload));
        }

        return new PayloadInfo(Blob: payload);
    }

    private static Dictionary<string, object?> BuildMetadata(IdentifiedMqttApplicationMessageReceivedEventArgs item)
    {
        var message = item.ApplicationMessage;
        return new Dictionary<string, object?>
        {
            ["messageId"] = item.MessageId,
            ["clientId"] = item.ClientId,
            ["processingFailed"] = item.ProcessingFailed,
            ["isEffectivelyRetained"] = item.IsEffectivelyRetained,
            ["isOwnMessage"] = item.IsOwnMessage,
            ["qos"] = (int)message.QualityOfServiceLevel,
            ["retain"] = message.Retain,
            ["payloadFormatIndicator"] = (int)message.PayloadFormatIndicator,
            ["contentType"] = message.ContentType,
            ["responseTopic"] = message.ResponseTopic,
            ["correlationDataHex"] = message.CorrelationData == null ? null : Convert.ToHexString(message.CorrelationData),
            ["messageExpiryInterval"] = message.MessageExpiryInterval
        };
    }

    private static bool TryParseJson(string? value, out IReadOnlyCollection<string> fields)
    {
        fields = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            fields = document.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
                .Select(p => p.Name)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Take(64)
                .ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryDecodeUtf8(byte[] payload)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string GetDefaultDatabasePath(string? exportPath)
    {
        var folder = string.IsNullOrWhiteSpace(exportPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrowsNestMqtt", "exports")
            : exportPath;
        return Path.Combine(folder, "crowsnest-auto-log.sqlite");
    }

    private static string NormalizeTopicFilter(string topicFilter) => topicFilter.Trim().TrimEnd('/');

    private static string SanitizeColumnName(string field)
    {
        var sanitized = Regex.Replace(field, "[^A-Za-z0-9_]+", "_").Trim('_').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "field";
        }

        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "field_" + sanitized;
        }

        return sanitized.Length > 64 ? sanitized[..64].Trim('_') : sanitized;
    }

    private static string BuildJsonPath(string field) => Regex.IsMatch(field, "^[A-Za-z_][A-Za-z0-9_]*$")
        ? "$." + field
        : "$.\"" + field.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string QuoteLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record PayloadInfo(
        string? Json = null,
        string? Text = null,
        string? Xml = null,
        string? Base64 = null,
        byte[]? Blob = null,
        IReadOnlyCollection<string>? JsonFields = null)
    {
        public IReadOnlyCollection<string> JsonFields { get; } = JsonFields ?? Array.Empty<string>();
    }
}
