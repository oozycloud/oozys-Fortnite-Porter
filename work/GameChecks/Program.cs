using System.Net;
using System.Text.Json;
using FortniteObj.Core;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : "recovery/game-checks");
Directory.CreateDirectory(root);
var progress = new Progress<string>(Console.WriteLine);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
if (args.Contains("usmap"))
{
    foreach (byte version in new byte[] { 4, 5 })
    foreach (bool versioned in new[] { false, true })
    {
        byte[] bytes = MappingFixture.Create(version, versioned);
        var provider = new CurrentUsmapProvider(bytes);
        Check((provider.EngineVersion != null) == (version == 5 && versioned), $"v{version} versioning={versioned}");
        provider.Reload();
    }
    foreach (byte[] bytes in new[] { new byte[] { 0xC4, 0x30, 6 }, new byte[] { 0xC4, 0x30, 5, 1 }, new byte[20] })
    {
        bool rejected = false;
        try { _ = new CurrentUsmapProvider(bytes); } catch (IOException) { rejected = true; }
        Check(rejected, "unknown or malformed mapping rejected");
    }
    foreach (string file in Directory.GetFiles(AppSettings.DataFolder, "*.usmap"))
    {
        var provider = new CurrentUsmapProvider(file);
        Check(provider.MappingsForGame.Types.Count > 1000, "live mapping " + Path.GetFileName(file));
        Console.WriteLine($"Types={provider.MappingsForGame.Types.Count} Enums={provider.MappingsForGame.Enums.Count} Engine={provider.EngineVersion}");
    }
    return;
}
if (args.Length > 1 && args[1] == "parts")
{
    using var catalog = JsonDocument.Parse(File.ReadAllText("recovery/pc-parts-check/catalog.json"));
    var assets = catalog.RootElement.GetProperty("Assets").Deserialize<List<Cosmetic>>()!;
    Check(assets.All(a => UnitySession.PartCategories.Contains(a.Category)), "catalog contains PC part models only");
    var categories = assets.Select(a => a.Category).Distinct().ToArray();
    foreach (string required in new[] { "GPUs", "Motherboards", "CPUs", "Coolers", "Fans", "Cases", "RAM", "Storage", "Power supplies" })
        Check(categories.Contains(required), required + " present");
    var results = new List<ExportResult>();
    foreach (string category in categories)
    {
        var asset = assets.First(a => a.Category == category);
        Console.WriteLine("TEST " + category + " " + asset.Name);
        ExportResult result;
        try { result = await new UnitySession().Export(asset, new AppSettings { ExportFolder = root, ExportFormat = "SOURCE", OnlyBlend = false }, progress, CancellationToken.None); }
        catch (IOException ex) when (ex.Message.Contains("no enabled static meshes"))
        {
            File.AppendAllText(Path.Combine(root, "unsupported-parts.txt"), asset.Category + ": " + asset.Name + Environment.NewLine);
            Console.WriteLine("UNSUPPORTED runtime geometry: " + asset.Name);
            continue;
        }
        Check(result.Meshes > 0 && Directory.EnumerateFiles(result.Folder, "*.blend").Any(), "Part export " + asset.Name);
        results.Add(result);
        File.WriteAllText(Path.Combine(root, "part-results.json"), JsonSerializer.Serialize(results));
    }
    foreach (string format in new[] { "GLB", "GLTF", "FBX", "OBJ", "STL" })
        await ExchangeExport.Create(results.First(r => r.Textures > 0), new AppSettings { ExportFormat = format, TriangulateMeshes = true }, progress, CancellationToken.None);
    return;
}
if (args.Length > 1 && args[1] == "unity")
{
    using var catalog = JsonDocument.Parse(File.ReadAllText("recovery/unity-check/catalog.json"));
    var assets = catalog.RootElement.GetProperty("Assets").Deserialize<List<Cosmetic>>()!;
    var selected = assets.Where(a => a.Category == "Meshes" && a.Name.Contains("RTX", StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
    selected.Add(assets.First(a => a.Category == "Textures" && a.Name == "GPU_NVIDIA_GEFORCE_RTX_4080"));
    selected.Add(assets.First(a => a.Category == "Sprites"));
    if (selected.Count < 4) selected.AddRange(assets.Where(a => a.Category == "Meshes").Take(2));
    var session = new UnitySession(); var results = new List<ExportResult>();
    foreach (var asset in selected)
    {
        Console.WriteLine("TEST " + asset.Name + " " + asset.Category);
        var result = await session.Export(asset, new AppSettings { ExportFolder = root, ExportFormat = "SOURCE", OnlyBlend = false }, progress, CancellationToken.None);
        Check(Directory.EnumerateFiles(result.Folder).Any(p => p.EndsWith(asset.Category == "Meshes" ? ".blend" : ".png")), "Export " + asset.Name);
        results.Add(result);
    }
    foreach (string format in new[] { "GLB", "GLTF", "FBX", "OBJ", "STL" })
        await ExchangeExport.Create(results.First(r => r.Meshes > 0), new AppSettings { ExportFormat = format, TriangulateMeshes = true }, progress, CancellationToken.None);
    File.WriteAllText(Path.Combine(root, "unity-results.json"), JsonSerializer.Serialize(results));
    return;
}
var handler = new Fixture();
using var http = new HttpClient(handler);
var cache = new CompatibilityCache(http, Path.Combine(root, "cache-" + Guid.NewGuid().ToString("N")));
var first = await cache.Refresh(progress, CancellationToken.None, true);
Check(!first.Cached && File.Exists(first.MappingPath), "fresh keys and mapping commit");
int count = handler.Calls;
await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => cache.Refresh(progress, CancellationToken.None)));
Check(count == handler.Calls, "scan reuses launch refresh without duplicate requests");
File.WriteAllBytes(first.MappingPath, new byte[32]);
Check(!(await cache.Refresh(progress, CancellationToken.None, true)).Cached, "damaged cached mapping is downloaded again");
foreach (string mode in new[] { "offline", "mismatch", "malformed", "badkey", "badmap", "badpayload" })
{
    handler.Mode = mode;
    var fallback = await cache.Refresh(progress, CancellationToken.None, true);
    Check(fallback.Cached && fallback.Version == "test-build-1", mode + " preserves matched cache");
}
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel(); bool cancelled = false;
    try { await cache.Refresh(progress, canceled.Token, true); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "cancellation never returns saved data");
}
var empty = new CompatibilityCache(http, Path.Combine(root, "empty-" + Guid.NewGuid().ToString("N")));
bool failed = false;
try { await empty.Refresh(progress, CancellationToken.None, true); } catch (IOException) { failed = true; }
Check(failed, "no cache returns actionable error");
using var liveHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
var live = await new CompatibilityCache(liveHttp, Path.Combine(root, "live")).Refresh(progress, CancellationToken.None, true);
Check(!live.Cached && File.Exists(live.MappingPath), "live public service and mapping download");
File.WriteAllText(Path.Combine(root, "compatibility-results.json"), JsonSerializer.Serialize(new { Passed = 12, LiveVersion = live.Version, KeysLogged = false }));

sealed class Fixture : HttpMessageHandler
{
    public string Mode = "ok"; public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        if (Mode == "offline") throw new HttpRequestException("Offline");
        string path = request.RequestUri!.AbsolutePath;
        string version = Mode is "badmap" or "badpayload" ? "test-build-2" : "test-build-1";
        HttpContent content;
        if (path.EndsWith("aes")) content = new StringContent(Mode == "malformed" ? "<html>" : JsonSerializer.Serialize(new { version, mainKey = Mode == "badkey" ? "invalid" : "0x" + new string('A', 64), dynamicKeys = Array.Empty<string>() }));
        else if (path.EndsWith("mappings")) content = new StringContent(JsonSerializer.Serialize(new { version = Mode == "mismatch" ? "other" : version, mappings = new { Brotli = "https://fixture.invalid/file.usmap" } }));
        else { var bytes = MappingFixture.Create(5, true); if (Mode == "badmap") bytes[0] = 0; if (Mode == "badpayload") bytes = bytes[..20]; content = new ByteArrayContent(bytes); }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}

static class MappingFixture
{
    public static byte[] Create(byte version, bool versioned)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x30C4); writer.Write(version); writer.Write(versioned ? 1 : 0);
        if (versioned)
        {
            if (version == 5)
            {
                writer.Write((ushort)5); writer.Write((ushort)8); writer.Write((ushort)0); writer.Write((uint)1234);
                writer.Write(5); writer.Write(new byte[] { 116, 101, 115, 116, 0 });
            }
            writer.Write(522); writer.Write(1018); writer.Write(0); writer.Write((uint)1234);
        }
        writer.Write((byte)0); writer.Write((uint)12); writer.Write((uint)12);
        writer.Write(new byte[12]);
        return stream.ToArray();
    }
}
