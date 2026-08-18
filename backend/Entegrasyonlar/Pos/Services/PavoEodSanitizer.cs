using System.Text.Json;
using System.Text.Json.Nodes;

namespace STYS.Entegrasyonlar.Pos.Services;

/// <summary>
/// Sanitizes a PAVO PerformEOD response before central persistence: nulls <c>data.eodImage</c>
/// (raw Base64) and recursively removes any <c>cardNo</c> property (case-insensitive) from the Data
/// tree so masked-or-not card data never reaches the central DB.
/// </summary>
public static class PavoEodSanitizer
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
            if (node is JsonObject root)
            {
                if (TryGetPropertyIgnoreCase(root, "Data", out var data))
                {
                    NullOutIgnoreCase(data, "eodImage");
                    RemoveCardNoRecursively(data);
                }
                else
                {
                    RemoveCardNoRecursively(root);
                }
            }

            return node?.ToJsonString(JsonOptions) ?? payload;
        }
        catch
        {
            return payload;
        }
    }

    /// <summary>Sanitizes a standalone eodData fragment for the EodDataJson column; returns null when absent.</summary>
    public static string? SanitizeEodData(JsonElement? eodData)
    {
        if (eodData is null || eodData.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(eodData.Value.GetRawText());
            if (node is JsonObject or JsonArray)
            {
                RemoveCardNoRecursively(node);
            }

            return node?.ToJsonString(JsonOptions) ?? eodData.Value.GetRawText();
        }
        catch
        {
            return eodData.Value.GetRawText();
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

    private static void RemoveCardNoRecursively(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (string.Equals(property.Key, "cardNo", StringComparison.OrdinalIgnoreCase))
                    {
                        obj.Remove(property.Key);
                    }
                    else if (property.Value is not null)
                    {
                        RemoveCardNoRecursively(property.Value);
                    }
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null)
                    {
                        RemoveCardNoRecursively(item);
                    }
                }

                break;
        }
    }
}
