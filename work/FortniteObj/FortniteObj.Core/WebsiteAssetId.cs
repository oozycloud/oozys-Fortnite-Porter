using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace FortniteObj.Core;

public sealed record WebsiteAssetId(bool Fortnite, string Id)
{
    public string PublicId { get; init; }
    public Dictionary<string, string> StyleValues { get; init; } = new();
    public Dictionary<string, string> StyleNames { get; init; } = new();
    public string Selection => StyleValues.Count == 0 ? "Default" : StyleSelection.Encode(StyleValues);
    private static readonly Lazy<Dictionary<string, JsonElement>> Registry = new(() =>
    {
        using Stream stream = typeof(WebsiteAssetId).Assembly.GetManifestResourceStream("website-id-registry.json")
            ?? throw new IOException("The website ID catalogue is missing from this installation.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("assets").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString(), x => x.Clone(), StringComparer.OrdinalIgnoreCase);
    });

    public static bool TryParse(string text, out WebsiteAssetId result)
    {
        result = null;
        var shortId = Regex.Match((text ?? "").Trim(), @"^([A-Z][1-9][0-9]{0,8})(?:S([1-9][0-9]{0,100}))?$", RegexOptions.IgnoreCase);
        if (shortId.Success)
        {
            if (!Registry.Value.TryGetValue(shortId.Groups[1].Value, out var entry)) return false;
            string source = entry.GetProperty("source").GetString();
            var values = new Dictionary<string, string>();
            var names = new Dictionary<string, string>();
            string publicId = entry.GetProperty("id").GetString();
            if (shortId.Groups[2].Success)
            {
                if (!entry.TryGetProperty("styleChannels", out var channels) || channels.GetArrayLength() == 0) return false;
                BigInteger ordinal = BigInteger.Parse(shortId.Groups[2].Value) - 1;
                foreach (var channel in channels.EnumerateArray())
                {
                    int index = (int)(ordinal % 1024); ordinal /= 1024;
                    var options = channel.GetProperty("options");
                    if (index >= options.GetArrayLength()) return false;
                    string key = channel.GetProperty("key").GetString();
                    values[key] = options[index].GetProperty("key").GetString();
                    names[key] = options[index].GetProperty("name").GetString();
                }
                if (ordinal != 0) return false;
                publicId += "S" + shortId.Groups[2].Value;
            }
            result = new WebsiteAssetId(source.StartsWith("fn:"), entry.GetProperty("assetId").GetString())
            { PublicId = publicId, StyleValues = values, StyleNames = names };
            return true;
        }
        var match = Regex.Match((text ?? "").Trim(), @"^(fn|pcbs2):([A-Za-z0-9_-]{1,160})$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        bool fortnite = match.Groups[1].Value.Equals("fn", StringComparison.OrdinalIgnoreCase);
        string id = match.Groups[2].Value;
        if (!fortnite && !Regex.IsMatch(id, "^[0-9a-fA-F]{20}$")) return false;
        result = new WebsiteAssetId(fortnite, id);
        return true;
    }

    public override string ToString() => PublicId ?? (Fortnite ? "fn:" : "pcbs2:") + Id;
}
