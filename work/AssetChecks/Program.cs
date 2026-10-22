using FortniteObj.Core;
using CUE4Parse.FileProvider;
using Newtonsoft.Json;
using System.Reflection;
if(args.Contains("--rocket-check")) {
 var rl=new RocketLeagueSession();var log=new LogProgress();var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
 var catalog=await rl.Scan(RocketLeagueSession.Detect()??throw new Exception("Rocket League not found"),log,CancellationToken.None);
 var car=catalog.Single(x=>x.Id=="RL23");var decal=rl.GetDecals(car).Single(x=>x.Name=="Octane: Stripes");
 var settings=new AppSettings{ExportFolder=folder,ExportFormat="GLB"};
 try {await rl.Export(car,"not-compatible",settings,log,CancellationToken.None);throw new Exception("Invalid decal accepted");} catch(IOException ex) when(ex.Message.Contains("not compatible")){}
 var result=await rl.Export(car,decal.Id,settings,log,CancellationToken.None);
 if(result.Meshes==0||result.Textures==0||!Directory.EnumerateFiles(result.Folder,"*.blend").Any()||!Directory.EnumerateFiles(Path.Combine(result.Folder,"GLB"),"*.glb").Any())throw new Exception("Rocket League packed export incomplete");
 File.WriteAllText(Path.Combine(folder,"validation.json"),System.Text.Json.JsonSerializer.Serialize(new {Count=catalog.Count,CompatibleDecals=rl.GetDecals(car).Count,Result=result},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS Rocket League real scan, compatible decal, invalid decal rejection, packed Blender and GLB.");return;
}
if(args.Contains("--website-id-checks")) {
 var versions = new CUE4Parse.UE4.Versions.VersionContainer(CUE4Parse.UE4.Versions.EGame.GAME_UE5_8);
 versions["SkeletalMesh.KeepMobileMinLODSettingOnDesktop"]=true;
 if(!((CUE4Parse.UE4.Versions.VersionContainer)versions.Clone())["SkeletalMesh.KeepMobileMinLODSettingOnDesktop"]) throw new Exception("Parser cloned away the game mesh setting.");

 using var stream=typeof(WebsiteAssetId).Assembly.GetManifestResourceStream("website-id-registry.json")!;
 using var document=System.Text.Json.JsonDocument.Parse(stream);
 int checkedIds=0, checkedStyles=0;
 foreach(var entry in document.RootElement.GetProperty("assets").EnumerateArray()) {
  string id=entry.GetProperty("id").GetString()!;
  if(!WebsiteAssetId.TryParse(id.ToLowerInvariant(),out var plain) || plain.Id!=entry.GetProperty("assetId").GetString() || plain.StyleValues.Count!=0) throw new Exception("Base ID mismatch: "+id);
  checkedIds++;
  if(!entry.TryGetProperty("styleChannels",out var channels) || channels.GetArrayLength()==0) continue;
  System.Numerics.BigInteger ordinal=0, place=1;
  foreach(var channel in channels.EnumerateArray()) { ordinal+=(channel.GetProperty("options").GetArrayLength()-1)*place;place*=1024; }
  if(!WebsiteAssetId.TryParse(id+"S"+(ordinal+1),out var styled)) throw new Exception("Style rejected: "+id);
  foreach(var channel in channels.EnumerateArray()) {
   var options=channel.GetProperty("options");var last=options[options.GetArrayLength()-1];
   if(styled.StyleValues[channel.GetProperty("key").GetString()!]!=last.GetProperty("key").GetString()) throw new Exception("Style mismatch: "+id);
  }
  if(WebsiteAssetId.TryParse(id+"S"+(place+1),out _)) throw new Exception("Overflow accepted: "+id);
  int count=channels[0].GetProperty("options").GetArrayLength();
  if(count<1024 && WebsiteAssetId.TryParse(id+"S"+(count+1),out _)) throw new Exception("Invalid style gap accepted: "+id);
  checkedStyles++;
 }
 Console.WriteLine($"PASS {checkedIds} exact IDs and {checkedStyles} complete style combinations, including invalid gaps and overflow.");return;
}
var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
using var session=new GameSession();
var progress=new LogProgress();
List<Cosmetic> list;
AbstractFileProvider provider;
if(args.Contains("--meshdiag") || args.Contains("--cached-export")) {
 var settings=AppSettings.Load();var compatibility=await Compatibility.Prepare(settings,progress,CancellationToken.None);
 var q=new QualityFileProvider(Installation.Paks(settings.GameFolder),new CUE4Parse.UE4.Versions.VersionContainer(Enum.Parse<CUE4Parse.UE4.Versions.EGame>(settings.EngineVersion), optionOverrides: args.Contains("--mobile-min") || args.Contains("--cached-export") ? new Dictionary<string,bool>{{"SkeletalMesh.KeepMobileMinLODSettingOnDesktop",true}} : null)) {MappingsContainer=new CurrentUsmapProvider(compatibility.MappingPath),EnablePackageStreaming=true,Progress=progress};
 q.Initialize();q.SubmitKeys(compatibility.Keys);q.PostMount();q.LoadVirtualPaths();File.WriteAllText(Path.Combine(output,"engine-config.json"),JsonConvert.SerializeObject(q.DefaultEngine,Formatting.Indented));Console.WriteLine("PROVIDER MOBILE MIN "+q.Versions["SkeletalMesh.KeepMobileMinLODSettingOnDesktop"]);provider=q;list=new();
 if(args.Contains("--cached-export")) {
  typeof(GameSession).GetField("_provider",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(session,q);
  list=System.Text.Json.JsonSerializer.Deserialize<List<Cosmetic>>(File.ReadAllText("recovery/site-ghoul/catalog.json"))!;
 }
} else {
 list=await session.Scan(AppSettings.Load(),progress,CancellationToken.None);
 provider=(AbstractFileProvider)typeof(GameSession).GetField("_provider",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
}
if(args.Contains("--cached-export")) {
 WebsiteAssetId.TryParse(args[2],out var requested);
 var selected=list.First(x=>x.Id==(requested?.Id ?? args[2]) || x.Name.Equals(args[2],StringComparison.OrdinalIgnoreCase));
 string selectedStyle=requested?.Selection ?? (args.Length>3?args[3]:"Default");
 if(requested!=null) {
  var actual=session.GetStyleChannels(selected);
  foreach(var choice in requested.StyleValues) {
   var option=actual.First(x=>x.Id==choice.Key).Options.First(x=>x.Id==choice.Value);
   if(option.Name!=requested.StyleNames[choice.Key])throw new Exception("Real style metadata does not match website ID");
  }
  Console.WriteLine("EXACT WEBSITE ID "+requested+" "+selected.Name+" "+selectedStyle);
 }

 if(args.Contains("--parts-only")) {session.InspectCharacterParts(selected,output);return;}
 if(args.Contains("--with-emote")) {
  var emote=list.First(x=>x.Category=="Animations" && x.Name.Equals(args[Array.IndexOf(args,"--with-emote")+1],StringComparison.OrdinalIgnoreCase));
  var scene=session.ExportScene(selected,emote,output,"Ghoul_Business_Hips",progress,CancellationToken.None,"PINK",matteMaterials:false);
  Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(scene));return;
 }
 var result=session.Export(selected,output,progress,CancellationToken.None,styleName:selectedStyle,matteMaterials:false);
 Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));return;
}
if(args.Contains("--website-icons")) {
 var registry=System.Text.Json.JsonDocument.Parse(File.ReadAllText("website/assets/id-registry.json"));
 var http=System.Text.Json.JsonDocument.Parse(File.ReadAllText("recovery/catalog-image-http.json"));
 int count=0;
 foreach(var entry in registry.RootElement.GetProperty("assets").EnumerateArray()) {
  if(!entry.GetProperty("source").GetString()!.StartsWith("fn:")) continue;
  if(entry.TryGetProperty("image",out var image) && http.RootElement.TryGetProperty(image.GetString()!,out var status) && status.GetInt32()==200) continue;
  var id=entry.GetProperty("id").GetString()!;var folder=Path.Combine(output,id);Directory.CreateDirectory(folder);
  if(Directory.GetFiles(folder,"*_preview.png").Length>0) continue;
  var item=list.First(x=>x.Id==entry.GetProperty("assetId").GetString());
  var result=session.AddPreviewPng(item,"Default",new ExportResult(folder,0,0,Array.Empty<string>()),CancellationToken.None);
  File.WriteAllText(Path.Combine(folder,"warnings.json"),System.Text.Json.JsonSerializer.Serialize(result.Warnings));
  Console.WriteLine("ICON "+(++count)+" "+id+" "+item.Name+" "+Directory.GetFiles(folder,"*_preview.png").Length);
 }
 return;
}
if(args.Contains("--meshdiag")) {
 CUE4Parse.Globals.FatalObjectSerializationErrors=true;
 ((QualityFileProvider)provider).EnablePackageStreaming=true;
 var files=provider.Files.Values.Where(x=>x.Path.EndsWith("/Historian_Male_Axe.uasset",StringComparison.OrdinalIgnoreCase)).ToArray();
 foreach(var file in files) {
  var package=(CUE4Parse.UE4.Assets.IoPackage)provider.LoadPackage(file);
  Console.WriteLine("MESHFILE "+file.Path);
  foreach(var reader in ((QualityFileProvider)provider).MountedVfs) {
   foreach(var found in reader.Files.Values.Where(f=>f.Path.Equals(file.Path,StringComparison.OrdinalIgnoreCase))) Console.WriteLine("ARCHIVE "+reader.Name+" size "+found.Size);
  }

  var io=(CUE4Parse.UE4.IO.Objects.FIoStoreEntry)file;
  Console.WriteLine("OPTIONAL "+provider.Files.TryGetOptionalSegmentPackage(io.ChunkId.AsPackageId(),out var opt)+" "+opt?.Path);
  var tocs=(CUE4Parse.UE4.IO.IoChunkToc[])typeof(Lazy<CUE4Parse.UE4.IO.IoChunkToc[]>).GetProperty("Value")!.GetValue(typeof(QualityFileProvider).GetField("_tocs",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(provider))!;
  foreach(var toc in tocs) foreach(var c in toc.OnDemandToc.Containers) for(ushort ci=0;ci<3;ci++) {
   var chunk=new CUE4Parse.UE4.IO.Objects.FIoChunkId(io.ChunkId.ChunkId,ci,CUE4Parse.UE4.IO.Objects.EIoChunkType5.ExportBundleData);
   if(c.TryGetFileEntryHash(chunk,out var hash))Console.WriteLine("STREAM "+c.ContainerName+" "+ci+" "+hash);
  }

  Console.WriteLine("BULK "+JsonConvert.SerializeObject(package.BulkDataMap));
  foreach(var mesh in ((CUE4Parse.UE4.Assets.IPackage)package).GetExports().OfType<CUE4Parse.UE4.Assets.Exports.SkeletalMesh.USkeletalMesh>()) {
   Console.WriteLine("MESH "+mesh.Name+" LODS "+JsonConvert.SerializeObject(mesh.LODModels?.Select(x=>new{x.NumVertices,x.SkipLod}))+" Nanite "+(mesh.NaniteResources!=null));
   Console.WriteLine("NANITE "+JsonConvert.SerializeObject(mesh.NaniteResources));
   File.WriteAllText(Path.Combine(output,"mesh.json"),JsonConvert.SerializeObject(mesh,Formatting.Indented));

  }
 }
 return;
}
File.WriteAllText(Path.Combine(output,"catalog.json"),JsonConvert.SerializeObject(list,Formatting.Indented));
if(args.Contains("--website-styles")) {
 var records = new Dictionary<string,object>();var errors=new List<string>();int count=0;
 foreach(var asset in list.Where(x=>new[]{"Skins","Back blings","Pickaxes","Gliders"}.Contains(x.Category))) {
  try { records[asset.Id] = session.GetStyleChannels(asset); }
  catch(Exception ex) { errors.Add(asset.Id+": "+ex.Message); }
  if(++count%100==0) Console.WriteLine("Style metadata "+count);
 }
 File.WriteAllText(Path.Combine(output,"style-channels.json"),JsonConvert.SerializeObject(records));
 File.WriteAllLines(Path.Combine(output,"style-errors.txt"),errors);
 Console.WriteLine("STYLE METADATA COMPLETE "+records.Count+"; errors "+errors.Count);return;
}
if(args.Contains("--paths")) {
 var paths=provider.Files.Values.Select(f=>f.Path).Where(p=>p.EndsWith(".uasset",StringComparison.OrdinalIgnoreCase)).ToArray();
 File.WriteAllLines(Path.Combine(output,"paths.txt"),paths.Where(p=>new[]{"/Weapons/","/Vehicles/","/Building/","/BuildingActors/","/PlayerBuilt/","/BuildingMeshes/"}.Any(t=>p.Contains(t,StringComparison.OrdinalIgnoreCase))));
}
File.WriteAllLines(Path.Combine(output,"all-paths.txt"),provider.Files.Values.Select(f=>f.Path));
if(args.Contains("--probe"))foreach(var path in args.Skip(2)) {
 try {File.WriteAllText(Path.Combine(output,Installation.SafeName(path.Split('/').Last())+".json"),JsonConvert.SerializeObject(provider.LoadPackage(path).GetExports(),Formatting.Indented));}catch(Exception ex){Console.WriteLine(ex.Message);}
}
if(args.Contains("--serve")) {
 ((QualityFileProvider)provider).EnablePackageStreaming=true;
 Console.WriteLine("READY FOR REQUESTS");
 while(!File.Exists(Path.Combine(output,"stop"))) {
  var request=Path.Combine(output,"request.json");if(!File.Exists(request)){await Task.Delay(250);continue;}
  var paths=JsonConvert.DeserializeObject<string[]>(File.ReadAllText(request))!;File.Delete(request);
  foreach(var path in paths) {
   try{
    if(path.StartsWith("EXPORT:")) {
      var id=path[7..];var item=list.First(x=>x.Id==id || x.Name==id);
      var result=session.Export(item,output,progress,CancellationToken.None,matteMaterials:false);
      File.WriteAllText(Path.Combine(output,Installation.SafeName(id)+"-result.json"),JsonConvert.SerializeObject(result,Formatting.Indented));continue;
    }
    ((QualityFileProvider)provider).EnsurePackage(path);File.WriteAllText(Path.Combine(output,Installation.SafeName(path.Split('/').Last())+".json"),JsonConvert.SerializeObject(provider.LoadPackage(path.Split('.')[0]).GetExports(),Formatting.Indented));Console.WriteLine("PROBED "+path);}
   catch(Exception ex){Console.WriteLine("ERROR "+path+" "+ex);File.WriteAllText(Path.Combine(output,Installation.SafeName(path)+"-error.txt"),ex.ToString());}
  }
  File.WriteAllText(Path.Combine(output,"done"),"done");
 }
}
class LogProgress:IProgress<string>{public void Report(string s)=>Console.WriteLine(s);}


