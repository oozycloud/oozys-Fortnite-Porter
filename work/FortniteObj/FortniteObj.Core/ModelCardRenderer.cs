using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace FortniteObj.Core;
internal static class ModelCardRenderer {
 static readonly SemaphoreSlim gate=new(1);
 static string Root=>Path.Combine(AppSettings.DataFolder,"card-renders");
 internal static async Task<string?> Render(Cosmetic asset,CancellationToken ct){
  bool rocket=asset.Id.StartsWith("RL",StringComparison.Ordinal);
  bool pc=UnitySession.PartCategories.Contains(asset.Category);
  if((!rocket&&!pc)||string.IsNullOrWhiteSpace(asset.ObjectPath))return null;
  if(rocket&&!File.Exists(asset.ObjectPath.Split('|')[0]))return null;
  string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(asset.Id+"|"+asset.ObjectPath)))[..24];Directory.CreateDirectory(Root);string target=Path.Combine(Root,key+".png");if(File.Exists(target))return target;
  await gate.WaitAsync(ct);try{
   if(File.Exists(target))return target;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));
   var settings=new AppSettings{ExportFolder=Root,ExportFormat="BLEND",OnlyBlend=true,ExportAnimations=false,TextureSizeLimit=512};
   var progress=new Progress<string>();ExportResult result=rocket?await new RocketLeagueSession().Export(asset,"Default",settings,progress,timeout.Token,preview:true):await new UnitySession().Export(asset,settings,progress,timeout.Token);
   string folder=Path.GetFullPath(result.Folder),root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
   if(!folder.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Preview output leaves its cache.");
   try{
    string preview=Path.Combine(folder,"preview.png");
    if(!File.Exists(preview)){
     string? blend=Directory.GetFiles(folder,"*.blend").FirstOrDefault();if(blend==null)return null;
     string blenderRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");string? blender=Directory.Exists(blenderRoot)?Directory.EnumerateFiles(blenderRoot,"blender.exe",SearchOption.AllDirectories).OrderDescending().FirstOrDefault():null;if(blender==null)return null;
     var start=new ProcessStartInfo(blender){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in new[]{"--background","--factory-startup","--disable-autoexec","--python-exit-code","1","--python",Path.Combine(AppContext.BaseDirectory,"model_card_render.py"),"--",blend,preview})start.ArgumentList.Add(a);
     using var process=Process.Start(start)??throw new IOException("Could not start preview renderer.");var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();try{await process.WaitForExitAsync(timeout.Token);}catch{if(!process.HasExited)process.Kill(true);await Task.WhenAll(output,error);throw;}await Task.WhenAll(output,error);if(process.ExitCode!=0)return null;
    }
    if(!File.Exists(preview))return null;File.Copy(preview,target,true);return target;
   }finally{Directory.Delete(folder,true);}
  }finally{gate.Release();}
 }
}
