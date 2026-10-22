using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public static class ExchangeExport
{
    public static readonly string[] Formats = { "BLEND", "SOURCE", "GLB", "GLTF", "FBX", "OBJ", "STL" };
    public static string Hint(string format) => format switch
    {
        "SOURCE" => "Packed Blender plus OBJ, textures and editable source data.",
        "GLB" => "GLB packs standard materials, textures, rig and animation into one file. Blender is also retained.",
        "GLTF" => "glTF uses a JSON file, a binary buffer and separate textures. Keep the exported folder together.",
        "FBX" => "FBX exports the model and rig with embedded textures and optional animation. Blender is also retained.",
        "OBJ" => "OBJ exports a static mesh with an MTL file and separate textures. No rig or animation.",
        "STL" => "STL exports static geometry in millimetres, without textures, rig or animation. Check watertightness before printing.",
        _ => "One .blend containing the model, textures, rig and animation."
    };

    public static async Task<ExportResult> Create(ExportResult result, AppSettings settings, IProgress<string> progress, CancellationToken ct)
    {
        string format = settings.ExportFormat;
        if (format is "BLEND" or "SOURCE") return result;
        if (!Formats.Contains(format)) throw new ArgumentException("Unknown export format: " + format);
        string blend = Directory.EnumerateFiles(result.Folder, "*.blend").SingleOrDefault()
            ?? throw new IOException("The packed Blender scene is missing; the requested exchange file cannot be created.");
        string blenderRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
        string blender = Directory.Exists(blenderRoot) ? Directory.EnumerateFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault()! : null!;
        if (blender == null) throw new IOException("Blender is required for this export format.");
        string exportName = Installation.SafeName(Path.GetFileNameWithoutExtension(blend));
        int nameBudget = 240 - Path.Combine(result.Folder, format).Length - 6;
        if (nameBudget < 12) throw new IOException("The export folder path is too long for Blender. Choose a shorter output folder in Settings.");
        exportName = exportName[..Math.Min(exportName.Length, nameBudget)];
        string stage = Path.Combine(Path.GetTempPath(), "oozy-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(stage);
        try
        {
        string inputBlend = blend;
        if (blend.Length > 220)
        {
            inputBlend = Path.Combine(stage, "source.blend");
            File.Copy(blend, inputBlend);
        }
        string script = Path.Combine(stage, "export.py");
        using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteObj.blender_exchange.py")!)
        using (var output = File.Create(script)) input.CopyTo(output);
        string request = Path.Combine(stage, "request.json");
        File.WriteAllText(request, JsonSerializer.Serialize(new { Format = format, Blend = inputBlend, Output = stage,
            Name = exportName, Animation = settings.ExportAnimations, Triangulate = settings.TriangulateMeshes,
            TextureLimit = settings.TextureSizeLimit }));
        progress.Report("Writing " + format + " export…");
            var start = new ProcessStartInfo(blender) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "--background", "--python-exit-code", "1", "--python", script, "--", request }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new IOException("Could not start Blender.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                File.WriteAllText(Path.Combine(result.Folder, "exchange.log"), await stdout + "\n" + await stderr);
            }
            ct.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(Path.Combine(stage, "complete.json")))
                throw new IOException(format + " conversion failed. The Blender scene is preserved; see exchange.log.");
            string destination = Path.Combine(result.Folder, format);
            if (Directory.Exists(destination)) throw new IOException("An exchange folder already exists: " + destination);
            File.Delete(script); File.Delete(request);
            if (inputBlend != blend) File.Delete(inputBlend);
            string pending = Path.Combine(result.Folder, ".exchange-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(pending);
            try
            {
                foreach (string directory in Directory.EnumerateDirectories(stage, "*", SearchOption.AllDirectories))
                    Directory.CreateDirectory(Path.Combine(pending, Path.GetRelativePath(stage, directory)));
                foreach (string file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    File.Copy(file, Path.Combine(pending, Path.GetRelativePath(stage, file)));
                }
                Directory.Move(pending, destination);
            }
            finally { if (Directory.Exists(pending)) Directory.Delete(pending, true); }
            progress.Report(format + " export saved.");
            return result with { Warnings = result.Warnings.Concat(new[] { format + ": custom Blender toon shaders, cloth simulation and procedural effects are not fully represented. The original .blend is retained." }).ToArray() };
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
    }
}
