using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace FortniteObj.Core;
public sealed class RocketLeagueSession
{
    public static readonly string[] Categories = { "Cars", "Decals", "Wheels", "Goal explosions", "Boosts", "Trails", "Toppers", "Antennas", "Maps", "Balls", "Paint finishes", "Engine audio", "Banners", "Avatar borders", "Anthems" };
    public sealed record Decal(string Id, string Name, string Package) { public override string ToString() => Name; }
    private sealed record Catalog(List<Cosmetic> Assets, Dictionary<string,List<Decal>> Styles, string[] Warnings);
    private Dictionary<string,List<Decal>> _styles = new();
    public string[] Warnings { get; private set; } = Array.Empty<string>();
    private static string Script(string name) => Path.Combine(AppContext.BaseDirectory,name);
    private static string Cache => Path.Combine(AppSettings.DataFolder,"rocketleague");
    public IReadOnlyList<Decal> GetDecals(Cosmetic asset) => _styles.TryGetValue(asset.Id,out var decals) ? decals : Array.Empty<Decal>();
    public static string? Detect()
    {
        string manifests=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests");
        if (Directory.Exists(manifests)) foreach (string file in Directory.EnumerateFiles(manifests,"*.item"))
        {
            try { using var j=JsonDocument.Parse(File.ReadAllText(file)); if(j.RootElement.GetProperty("DisplayName").GetString()?.Contains("Rocket League",StringComparison.OrdinalIgnoreCase)==true) { string path=j.RootElement.GetProperty("InstallLocation").GetString()!; if(Directory.Exists(path)) return path; } } catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        }
        foreach(string path in new[]{ Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Epic Games","rocketleague"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam","steamapps","common","rocketleague")}) if(Directory.Exists(Path.Combine(path,"TAGame","CookedPCConsole")))return path;
        return null;
    }
    public async Task RefreshCompatibility(CancellationToken ct)
    {
        Directory.CreateDirectory(Cache); string request=Path.Combine(Cache,"launch-"+Guid.NewGuid().ToString("N")+".json");
        try { await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new {Action="keys",Cache}),ct); await Run(Script(Path.Combine("unity-runtime","python.exe")),new[]{"-X","utf8",Script("rocketleague_assets.py"),request},new Progress<string>(),ct); }
        finally {File.Delete(request);}
    }
    public async Task<List<Cosmetic>> Scan(string root,IProgress<string> progress,CancellationToken ct)
    {
        string stage=Path.Combine(AppSettings.DataFolder,"rl-scan-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try {
            string result=Path.Combine(stage,"catalog.json"),request=Path.Combine(stage,"request.json");
            await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new {Action="scan",Root=root,Cache,Result=result}),ct);
            await Run(Script(Path.Combine("unity-runtime","python.exe")),new[]{"-X","utf8","-u",Script("rocketleague_assets.py"),request},progress,ct);
            var catalog=JsonSerializer.Deserialize<Catalog>(await File.ReadAllTextAsync(result,ct))??throw new IOException("Rocket League scan returned no catalogue.");
            _styles=catalog.Styles;Warnings=catalog.Warnings;progress.Report($"Loaded {catalog.Assets.Count:N0} Rocket League assets.");return catalog.Assets;
        } finally { Directory.Delete(stage,true); }
    }
    public async Task<ExportResult> Export(Cosmetic asset,string style,AppSettings settings,IProgress<string> progress,CancellationToken ct)
    {
        var decal=string.IsNullOrEmpty(style)||style=="Default"?null:GetDecals(asset).FirstOrDefault(d=>d.Id==style);
        if(!string.IsNullOrEmpty(style)&&style!="Default"&&decal==null)throw new IOException("That decal is not compatible with this car in the scanned catalogue.");
        string root=Path.Combine(settings.ExportFolder,"Rocket League");Directory.CreateDirectory(root);
        if(root.Length>140)throw new IOException("Choose a shorter export folder for Rocket League.");
        string name=Installation.SafeName(asset.Name);name=name[..Math.Min(45,name.Length)];
        string stage=Path.Combine(root,".export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string destination=Path.Combine(root,name+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")[..6]);
        try {
            string resultPath=Path.Combine(stage,"export-report.json"),request=Path.Combine(stage,"request.json");
            await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new {Action="export",Asset=asset,Decal=decal,Output=stage,Name=name,Cache,Result=resultPath}),ct);
            progress.Report("Reading Rocket League: "+asset.Name);
            await Run(Script(Path.Combine("unity-runtime","python.exe")),new[]{"-X","utf8","-u",Script("rocketleague_assets.py"),request},progress,ct);
            using(var report=JsonDocument.Parse(await File.ReadAllTextAsync(resultPath,ct))) if(report.RootElement.GetProperty("Meshes").GetInt32()>0) {
                string blenderRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");
                string? blender=Directory.Exists(blenderRoot)?Directory.EnumerateFiles(blenderRoot,"blender.exe",SearchOption.AllDirectories).OrderDescending().FirstOrDefault():null;
                if(blender==null)throw new IOException("Install Blender to create the Rocket League model scene.");
                progress.Report("Packing Rocket League meshes and textures…");
                await Run(blender,new[]{"--background","--factory-startup","--python-exit-code","1","--python",Script("rocketleague_blend.py"),"--",request},progress,ct);
            }
            using var final=JsonDocument.Parse(await File.ReadAllTextAsync(resultPath,ct));
            var result=new ExportResult(stage,final.RootElement.GetProperty("Meshes").GetInt32(),final.RootElement.GetProperty("Textures").GetInt32(),final.RootElement.GetProperty("Warnings").EnumerateArray().Select(x=>x.GetString()!).ToArray());
            if(result.Meshes>0)result=await ExchangeExport.Create(result,settings,progress,ct);
            File.Delete(request);ct.ThrowIfCancellationRequested();Directory.Move(stage,destination);return result with {Folder=destination};
        } finally {if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    private static async Task Run(string executable,IEnumerable<string> arguments,IProgress<string> progress,CancellationToken ct)
    {
        if(!File.Exists(executable))throw new FileNotFoundException("Rocket League export tools are missing. Repair the app.",executable);
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in arguments)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new IOException("Could not start Rocket League reader.");
        var output=Task.Run(async()=>{while(await process.StandardOutput.ReadLineAsync() is string line)if(line.StartsWith("Reading Rocket League"))progress.Report(line);});var error=process.StandardError.ReadToEndAsync();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();await Task.WhenAll(output,error);if(ct.IsCancellationRequested)throw;throw new IOException("Rocket League reader timed out.");}
        await output;string details=await error;if(process.ExitCode!=0)throw new IOException("Rocket League export failed: "+details[..Math.Min(details.Length,2000)]);
    }
}
