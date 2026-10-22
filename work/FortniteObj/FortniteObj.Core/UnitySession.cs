using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public sealed class UnitySession
{
    public static readonly string[] PartCategories = { "GPUs", "Motherboards", "CPUs", "Coolers", "Fans", "Cases", "RAM", "Storage", "Power supplies", "Water cooling", "Radiators", "Cables", "Fittings" };
    private sealed record Catalog(List<Cosmetic> Assets, string[] Warnings);
    public string[] Warnings { get; private set; } = Array.Empty<string>();
    private static string Runtime => Path.Combine(AppContext.BaseDirectory, "unity-runtime", "python.exe");
    private static string Script(string name) => Path.Combine(AppContext.BaseDirectory, name);

    public static string? DetectPcBuildingSimulator()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(folder)) return null;
        foreach (string file in Directory.EnumerateFiles(folder, "*.item"))
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(file));
                if (json.RootElement.TryGetProperty("DisplayName", out var name) && name.GetString() == "PC Building Simulator 2")
                {
                    string path = json.RootElement.GetProperty("InstallLocation").GetString()!;
                    if (Directory.Exists(path)) return path;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException) { }
        }
        return null;
    }

    public async Task<List<Cosmetic>> Scan(string root, IProgress<string> progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) throw new DirectoryNotFoundException("Choose the PC Building Simulator 2 installation folder.");
        string data = Path.Combine(root, "PCBS2_Data");
        if (!Directory.Exists(data)) data = root;
        if (!File.Exists(Path.Combine(data, "globalgamemanagers"))) throw new IOException("Choose the PC Building Simulator 2 folder or its PCBS2_Data folder.");
        string temporary = Path.Combine(AppSettings.DataFolder, "unity-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string result = Path.Combine(temporary, "catalog.json");
            string request = Path.Combine(temporary, "request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new { Action = "scan", Root = data, Result = result, IndexFile = Path.Combine(AppSettings.DataFolder, "pcbs-parts-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(data).ToLowerInvariant())))[..16] + ".json") }), ct);
            await Run(Runtime, new[] { "-X", "utf8", "-u", Script("unity_assets.py"), request }, progress, ct);
            var catalog = JsonSerializer.Deserialize<Catalog>(await File.ReadAllTextAsync(result, ct)) ?? throw new IOException("Unity scan returned no catalog.");
            Warnings = catalog.Warnings;
            progress.Report($"Loaded {catalog.Assets.Count:N0} PC part models; {Warnings.Length} read warnings.");
            return catalog.Assets;
        }
        finally { Directory.Delete(temporary, true); }
    }

    public async Task<ExportResult> Export(Cosmetic asset, AppSettings settings, IProgress<string> progress, CancellationToken ct)
    {
        string root = Path.Combine(settings.ExportFolder, "PC Building Simulator 2");
        Directory.CreateDirectory(root);
        int nameBudget = Math.Min(48, (240 - root.Length - asset.Id.Length - 40) / 2);
        if (nameBudget < 12) throw new IOException("The export folder path is too long for Blender. Choose a shorter output folder in Settings.");
        string name = Installation.SafeName(asset.Name);
        name = name[..Math.Min(name.Length, nameBudget)];
        string destination = Path.Combine(root, name + "_" + asset.Id);
        if (Directory.Exists(destination)) destination += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
        string stage = Path.Combine(root, ".export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            string resultPath = Path.Combine(stage, "export-report.json"), requestPath = Path.Combine(stage, "request.json");
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(new { Action = "export", Asset = asset, Output = stage, Name = name, Result = resultPath, Format = settings.ExportFormat }), ct);
            progress.Report("Reading Unity asset: " + asset.Name);
            await Run(Runtime, new[] { "-X", "utf8", "-u", Script("unity_assets.py"), requestPath }, progress, ct);
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath, ct));
            int meshes = report.RootElement.GetProperty("Meshes").GetInt32();
            int textures = report.RootElement.GetProperty("Textures").GetInt32();
            string[] warnings = report.RootElement.GetProperty("Warnings").EnumerateArray().Select(x => x.GetString()!).ToArray();
            var result = new ExportResult(stage, meshes, textures, warnings);
            if (meshes > 0)
            {
                string blenderRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
                string? blender = Directory.Exists(blenderRoot) ? Directory.EnumerateFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault() : null;
                if (blender == null) throw new IOException("Install Blender to convert Unity meshes to the selected format.");
                progress.Report("Creating Blender scene…");
                await Run(blender, new[] { "--background", "--factory-startup", "--python-exit-code", "1", "--python", Script("unity_blend.py"), "--", requestPath }, progress, ct);
                result = await ExchangeExport.Create(result, settings, progress, ct);
                if (settings.ExportFormat != "SOURCE")
                {
                    File.Delete(Path.Combine(stage, name + ".obj"));
                    File.Delete(Path.Combine(stage, name + ".mtl"));
                    File.Delete(Path.Combine(stage, "part-scene.json"));
                    foreach (string texture in Directory.EnumerateFiles(stage, "texture_*.png")) File.Delete(texture);
                }
            }
            ct.ThrowIfCancellationRequested();
            File.Delete(requestPath);
            Directory.Move(stage, destination);
            return result with { Folder = destination };
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    private static async Task Run(string executable, IEnumerable<string> arguments, IProgress<string> progress, CancellationToken ct)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("A required export tool is missing. Repair the application with its installer.", executable);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the asset reader.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is string line)
                if (line.StartsWith("Reading Unity files ")) progress.Report(line);
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync();
            await Task.WhenAll(outputTask, errorTask);
            if (ct.IsCancellationRequested) throw;
            throw new IOException("The asset reader timed out. Try scanning a smaller asset folder.");
        }
        await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0) throw new IOException("Asset reader failed: " + error[..Math.Min(error.Length, 2000)]);
    }
}
