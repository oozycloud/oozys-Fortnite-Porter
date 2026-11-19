using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FortniteObj.Core;
using SkiaSharp;
namespace FortniteObj;
public sealed class AssetThumbnail : Image {
 public static readonly DependencyProperty AssetProperty=DependencyProperty.Register(nameof(Asset),typeof(Cosmetic),typeof(AssetThumbnail),new PropertyMetadata(null,Changed));
 public static readonly DependencyProperty StatusProperty=DependencyProperty.Register(nameof(Status),typeof(string),typeof(AssetThumbnail),new PropertyMetadata("Loading preview…"));
 public static readonly DependencyProperty AllowRenderProperty=DependencyProperty.Register(nameof(AllowRender),typeof(bool),typeof(AssetThumbnail),new PropertyMetadata(false,Changed));
 public bool AllowRender{get=>(bool)GetValue(AllowRenderProperty);set=>SetValue(AllowRenderProperty,value);}
 bool customPreview;
 public void SetPreviewState(string status){customPreview=true;request++;Source=null;Status=status;}
 public void Refresh(){customPreview=false;Load();}
 public void SetPreview(BitmapSource image){customPreview=true;request++;Source=image;Status="";}
 public Cosmetic? Asset{get=>(Cosmetic?)GetValue(AssetProperty);set=>SetValue(AssetProperty,value);}public string Status{get=>(string)GetValue(StatusProperty);private set=>SetValue(StatusProperty,value);}
 long request;
 public AssetThumbnail(){Stretch=Stretch.Uniform;Loaded+=(_,_)=>Load();Unloaded+=(_,_)=>{request++;};}
 static void Changed(DependencyObject value,DependencyPropertyChangedEventArgs e){var thumbnail=(AssetThumbnail)value;if(e.Property==AssetProperty)thumbnail.customPreview=false;thumbnail.Load();}
 async void Load(){if(customPreview)return;long revision=++request;Source=null;Status="Loading preview…";var asset=Asset;if(asset==null)return;try{var image=await AssetArtwork.Get(asset,AllowRender);if(revision!=request||Asset!=asset)return;Source=image;Status=image==null?"Source preview unavailable":"";}catch{if(revision==request)Status="Preview unavailable";}}
}
internal static class AssetArtwork {
 static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(20)};
 static readonly SemaphoreSlim network=new(4),native=new(1);
 static readonly ConcurrentDictionary<string,Task<BitmapSource?>> pending=new();
 static readonly ConcurrentDictionary<string,BitmapSource> memory=new();
 static readonly Lazy<Dictionary<string,string>> registry=new(()=>{
  using var stream=typeof(WebsiteAssetId).Assembly.GetManifestResourceStream("website-id-registry.json");if(stream==null)return new();using var doc=JsonDocument.Parse(stream);var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var a in doc.RootElement.GetProperty("assets").EnumerateArray())if(a.TryGetProperty("image",out var img)&&!string.IsNullOrWhiteSpace(img.GetString()))map.TryAdd(a.GetProperty("assetId").GetString()!,new Uri(new Uri("https://oozycloud.github.io/oozys-Fortnite-Porter/"),img.GetString()!).AbsoluteUri);
  return map;
 });
 internal static string Cache=>Path.Combine(AppSettings.DataFolder,"card-artwork");
 internal static Func<Cosmetic,Task<string?>>? RenderFallback {get;set;}
 internal static CancellationToken Lifetime {get;set;}
 internal static Task<BitmapSource?> Get(Cosmetic asset,bool allowRender=false){string key=asset.Id+"|"+asset.ObjectPath+"|"+asset.ImageUrl;if(memory.TryGetValue(key,out var cached))return Task.FromResult<BitmapSource?>(cached);return pending.GetOrAdd(key+"|"+allowRender,_=>Load(asset,key,allowRender));}
 static async Task<BitmapSource?> Load(Cosmetic a,string key,bool allowRender){
  try{
   Directory.CreateDirectory(Cache);string target=Path.Combine(Cache,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32]+".png");
   if(File.Exists(target))return Remember(key,await Task.Run(()=>Decode(File.ReadAllBytes(target))));
   var sources=new List<string>();if(!string.IsNullOrWhiteSpace(a.ImageUrl))sources.Add(a.ImageUrl);if(registry.Value.TryGetValue(a.Id,out var url))sources.Add(url);
   string local=Path.Combine(AppSettings.DataFolder,"previews",Installation.SafeName(a.Id)+".png");if(File.Exists(local))sources.Insert(0,local);
   foreach(string source in sources.Distinct())try{
    byte[] bytes;if(Uri.TryCreate(source,UriKind.Absolute,out var uri)&&uri.IsFile)bytes=await File.ReadAllBytesAsync(uri.LocalPath);
    else if(File.Exists(source))bytes=await File.ReadAllBytesAsync(source);
    else{if(uri?.Scheme!="https")continue;await network.WaitAsync();try{using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>20_000_000)continue;bytes=await response.Content.ReadAsByteArrayAsync();if(bytes.Length>20_000_000)continue;}finally{network.Release();}}
    var result=await Task.Run(()=>Normalize(bytes,target));if(result!=null)return Remember(key,result);
   }catch(Exception ex)when(ex is HttpRequestException or IOException or TaskCanceledException or ArgumentException){ }
   if(a.Id.StartsWith("RL",StringComparison.Ordinal)&&File.Exists(a.ObjectPath.Split('|')[0])){
    await native.WaitAsync();try{string? file=await RocketLeagueSession.Thumbnail(a,Lifetime);if(file!=null){var image=await Task.Run(()=>Normalize(File.ReadAllBytes(file),target));if(image!=null)return Remember(key,image);}}finally{native.Release();}
   }
   if(allowRender&&RenderFallback!=null){string? file=await RenderFallback(a);if(file!=null&&File.Exists(file))return Remember(key,await Task.Run(()=>Normalize(File.ReadAllBytes(file),target)));}
   return null;
  }catch{return null;}finally{pending.TryRemove(key+"|"+allowRender,out _);}
 }
 static BitmapSource? Remember(string key,BitmapSource? image){if(image!=null){if(memory.Count>200)memory.Clear();memory[key]=image;}return image;}
 static BitmapSource? Normalize(byte[] bytes,string target){using var original=SKBitmap.Decode(bytes);if(original==null)return null;float factor=Math.Min(1f,512f/Math.Max(original.Width,original.Height));using var resized=original.Resize(new SKImageInfo(Math.Max(1,(int)(original.Width*factor)),Math.Max(1,(int)(original.Height*factor))),SKFilterQuality.Medium);using var image=SKImage.FromBitmap(resized??original);using var data=image.Encode(SKEncodedImageFormat.Png,100);byte[] png=data.ToArray();string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,png);File.Move(temp,target,true);}finally{if(File.Exists(temp))File.Delete(temp);}return Decode(png);}
 static BitmapSource Decode(byte[] bytes){using var input=new MemoryStream(bytes);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=input;image.DecodePixelWidth=512;image.EndInit();image.Freeze();return image;}
}
