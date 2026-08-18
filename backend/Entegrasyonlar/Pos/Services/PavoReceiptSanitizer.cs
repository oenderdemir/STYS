using System.Text.Json;
using System.Text.Json.Nodes;

namespace STYS.Entegrasyonlar.Pos.Services;

/// <summary>
/// Removes receipt image Base64 fields from a PAVO payment result payload before it is persisted
/// centrally. The Agent serializes responses with <c>JsonSerializerDefaults.Web</c>, so the Data
/// object and its fields arrive camelCased (e.g. <c>data.customerReceiptImage</c>); property lookup is
/// therefore case-insensitive. The output JSON preserves all other fields verbatim.
/// </summary>
public static class PavoReceiptSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? Sanitize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        try
        {
            var node = JsonNode.Parse(payload);
            if (node is JsonObject root && TryGetPropertyIgnoreCase(root, "Data", out var data))
            {
                NullOutIgnoreCase(data, "customerReceiptImage");
                NullOutIgnoreCase(data, "merchantReceiptImage");
                NullOutIgnoreCase(data, "errorReceiptImage");
            }

            return node?.ToJsonString(JsonOptions) ?? payload;
        }
        catch
        {
            return payload;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonObject obj, string name, out JsonObject value)
    {
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase) && property.Value is JsonObject child)
            {
                value = child;
                return true;
            }
        }

        value = null!;
        return false;
    }

    private static void NullOutIgnoreCase(JsonObject obj, string name)
    {
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                obj[property.Key] = null;
                return;
            }
        }
    }
}
