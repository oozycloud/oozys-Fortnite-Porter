using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace FortniteObj.Core;
internal static class SkinCustomizationRenderer
{
 internal sealed record Result(string Path,string[] Warnings);
 internal static string CacheKey(Cosmetic asset,string selection,AppSettings settings)
 {
  string canonical=selection.StartsWith(StyleSelection.Prefix,StringComparison.Ordinal)?JsonSerializer.Serialize(StyleSelection.Decode(selection).OrderBy(x=>x.Key,StringComparer.Ordinal)):selection;
  string mapping=Stamp(settings.MappingFile)+"|"+(Directory.Exists(AppSettings.DataFolder)?string.Join("|",Directory.EnumerateFiles(AppSettings.DataFolder,"*.usmap",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Select(Stamp)):"");
  string paks=Path.Combine(settings.GameFolder,"FortniteGame","Content","Paks");
  if(!Directory.Exists(paks))paks=settings.GameFolder;
  string source=Directory.Exists(paks)?string.Join("|",Directory.EnumerateFiles(paks).Where(p=>Path.GetExtension(p) is ".utoc" or ".pak" or ".sig").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Select(Stamp)):paks;
  return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{asset.Id,asset.ObjectPath,asset.PackagePath,Selection=canonical,settings.GameFolder,settings.EngineVersion,settings.MatteMaterials,Mapping=mapping,Source=source,Version=PorterRelease.ProductInfo.Version,Renderer=2}))));
 }
 static string Stamp(string path)=>File.Exists(path)?$"{Path.GetFullPath(path)}:{new FileInfo(path).Length}:{File.GetLastWriteTimeUtc(path).Ticks}":path;
 internal static string Root=>Path.Combine(AppSettings.DataFolder,"skin-customization-previews");
 internal static async Task<Result> Render(GameSession session,Cosmetic asset,string selection,AppSettings settings,IProgress<string> progress,CancellationToken ct)
 {
  Directory.CreateDirectory(Root);string target=Path.Combine(Root,CacheKey(asset,selection,settings)+".png");if(File.Exists(target)){string meta=target+".json";return new(target,File.Exists(meta)?JsonSerializer.Deserialize<string[]>(File.ReadAllText(meta))??[]:[]);}
  string blenderRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");
  string? blender=Directory.Exists(blenderRoot)?Directory.EnumerateFiles(blenderRoot,"blender.exe",SearchOption.AllDirectories).OrderDescending().FirstOrDefault():null;
  if(blender==null)throw new IOException("Install Blender to preview your customizations.");
  // The caller serializes this mounted provider with scans, lookups and real exports.
  var result=await Task.Run(()=>session.Export(asset,Root,progress,ct,selection,false,settings.MatteMaterials,0,false),ct);
  string folder=Path.GetFullPath(result.Folder),owned=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
  if(!folder.StartsWith(owned,StringComparison.OrdinalIgnoreCase))throw new IOException("Preview output leaves its cache.");
  try
  {
   var warnings=result.Warnings.ToList();
   await Task.Run(()=>BlenderExport.Create(folder,warnings,progress,ct,required:true),ct);
   string? blend=Directory.GetFiles(folder,"*.blend").FirstOrDefault();if(blend==null)throw new IOException("The selected style could not be converted to a preview.");
   string preview=Path.Combine(folder,"customization-preview.png");
   var start=new ProcessStartInfo(blender){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(string a in new[]{"--background","--factory-startup","--disable-autoexec","--python-exit-code","1","--python",Path.Combine(AppContext.BaseDirectory,"model_card_render.py"),"--",blend,preview,"--skin"})start.ArgumentList.Add(a);
   using var process=Process.Start(start)??throw new IOException("Could not start the preview renderer.");
   var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
   try{await process.WaitForExitAsync(ct);}finally{if(!process.HasExited)process.Kill(true);await Task.WhenAll(output,error);}
   string renderLog=target+".log";await File.WriteAllTextAsync(renderLog,(await output)+"\n"+(await error));
   ct.ThrowIfCancellationRequested();
   if(process.ExitCode!=0||!File.Exists(preview))throw new IOException("The selected style could not be rendered. Try again. Renderer details are in the preview cache.");
   File.Copy(preview,target,true);File.WriteAllText(target+".json",JsonSerializer.Serialize(warnings));return new(target,warnings.ToArray());
  }
  finally
  {
   // Only this unique, verified exporter-owned cache folder is removed.
   try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}
