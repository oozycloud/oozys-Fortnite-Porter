using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

// Commit keys and mappings as a pair, after validating and downloading both.
public sealed class CompatibilityCache(HttpClient http, string folder)
{
    public sealed record Snapshot(JsonNode Aes, JsonNode Mappings, string MappingPath, bool Cached, DateTimeOffset CheckedAt)
    {
        public string Version => Aes["version"]!.GetValue<string>();
    }
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot? latest;

    public async Task<Snapshot> Refresh(IProgress<string> progress, CancellationToken ct, bool force = false)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!force && latest != null && DateTimeOffset.UtcNow - latest.CheckedAt < TimeSpan.FromMinutes(2)) return latest;
            Directory.CreateDirectory(folder);
            try
            {
                progress.Report("Checking current Fortnite keys and mappings…");
                Task<string> keysTask = http.GetStringAsync("https://uedb.dev/svc/api/v1/fortnite/aes", ct);
                Task<string> mappingsTask = http.GetStringAsync("https://uedb.dev/svc/api/v1/fortnite/mappings", ct);
                await Task.WhenAll(keysTask, mappingsTask);
                JsonNode aes = JsonNode.Parse(await keysTask)!;
                JsonNode mappings = JsonNode.Parse(await mappingsTask)!;
                Uri download = Validate(aes, mappings);
                string mapping = MappingPath(aes);
                bool usable = false;
                if (File.Exists(mapping))
                {
                    try { ValidateMapping(await File.ReadAllBytesAsync(mapping, ct)); usable = true; }
                    catch (IOException) { /* Retry a damaged or outdated cached download. */ }
                }
                if (!usable)
                {
                    progress.Report("Downloading current Fortnite mappings…");
                    byte[] data = await http.GetByteArrayAsync(download, ct);
                    if (data.Length < 16 || data[0] != 0xC4 || data[1] != 0x30)
                        throw new IOException("The service did not return a valid mapping file.");
                    // Validate the complete mapping before replacing a usable cache.
                    ValidateMapping(data);
                    await AtomicWrite(mapping, data, ct);
                }
                var pair = new JsonObject { ["aes"] = aes.DeepClone(), ["mappings"] = mappings.DeepClone() };
                await AtomicWrite(Path.Combine(folder, "compatibility.json"), System.Text.Encoding.UTF8.GetBytes(pair.ToJsonString()), ct);
                return latest = new(aes, mappings, mapping, false, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or JsonException or IOException or InvalidOperationException or FormatException)
            {
                // Never replace a known good cache with an error page or a mismatched pair.
                JsonNode aes, mappings;
                string pairPath = Path.Combine(folder, "compatibility.json");
                if (File.Exists(pairPath))
                {
                    JsonNode pair = JsonNode.Parse(await File.ReadAllTextAsync(pairPath, ct))!;
                    aes = pair["aes"]!;
                    mappings = pair["mappings"]!;
                }
                else if (File.Exists(Path.Combine(folder, "aes.json")) && File.Exists(Path.Combine(folder, "mappings.json")))
                {
                    aes = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "aes.json"), ct))!;
                    mappings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "mappings.json"), ct))!;
                }
                else throw new IOException("Fortnite compatibility could not be refreshed and no saved copy is available. Check your connection and scan again.", ex);
                Validate(aes, mappings);
                string mapping = MappingPath(aes);
                if (!File.Exists(mapping) || new FileInfo(mapping).Length < 16)
                    throw new IOException("The saved Fortnite mapping file is missing. Connect to the internet and scan again.", ex);
                ValidateMapping(await File.ReadAllBytesAsync(mapping, ct));
                progress.Report("Refresh unavailable; using saved Fortnite keys and mappings for " + aes["version"]!.GetValue<string>() + ".");
                return latest = new(aes, mappings, mapping, true, DateTimeOffset.UtcNow);
            }
        }
        finally { gate.Release(); }
    }

    private string MappingPath(JsonNode aes)
    {
        string version = aes["version"]!.GetValue<string>();
        return Path.Combine(folder, Installation.SafeName(version) + ".usmap");
    }

    private static void ValidateMapping(byte[] data)
    {
        // The provider's byte overload shares the same validation used at scan time.
        _ = new CurrentUsmapProvider(data);
    }

    private static Uri Validate(JsonNode aes, JsonNode mappings)
    {
        string version = aes?["version"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(version) || version != mappings?["version"]?.GetValue<string>())
            throw new IOException("The key and mapping service returned different builds.");
        ValidateKey(aes!["mainKey"]?.GetValue<string>());
        if (aes["dynamicKeys"] is JsonArray dynamicKeys)
            foreach (JsonNode entry in dynamicKeys)
            {
                if (!Regex.IsMatch(entry?["guid"]?.GetValue<string>() ?? "", "^[0-9a-fA-F]{32}$"))
                    throw new IOException("Invalid dynamic key identifier.");
                ValidateKey(entry!["key"]?.GetValue<string>());
            }
        string url = mappings!["mappings"]?["Brotli"]?.GetValue<string>() ?? mappings["mappings"]?["ZStandard"]?.GetValue<string>() ?? "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != "https")
            throw new IOException("Mapping download must use HTTPS.");
        return uri;
    }
    private static void ValidateKey(string? key)
    {
        if (!Regex.IsMatch(key ?? "", "^(0[xX])?[0-9a-fA-F]{64}$")) throw new IOException("Invalid AES key response.");
    }
    private static async Task AtomicWrite(string path, byte[] bytes, CancellationToken ct)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
