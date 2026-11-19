using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
namespace EmoteStudio;
public sealed class ClipInfo {public string name {get;set;}="";public int frames{get;set;}public double fps{get;set;}public double duration{get;set;}public bool loop{get;set;}}
public sealed class EmoteInfo {public string id{get;set;}=Guid.NewGuid().ToString("N");public string name{get;set;}="";public string path{get;set;}="";public string payload{get;set;}="";public List<ClipInfo> clips{get;set;}=[];public string[] warnings{get;set;}=[];}
public sealed class StudioProject {public int schema{get;set;}=1;public string projectName{get;set;}="Untitled project";public string id{get;set;}=Guid.NewGuid().ToString("N");public JsonObject? model{get;set;}public string rig{get;set;}="";public List<EmoteInfo> emotes{get;set;}=[];public string? selectedEmote{get;set;}public int selectedClip{get;set;}public Dictionary<string,string> boneMap{get;set;}=[];public Dictionary<string,double> wheelPose{get;set;}=[];}
public partial class MainWindow:Window {
 const string Version="beta 0.12.0";
 readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"oozycloud","EmoteStudio");
 string Projects=>Path.Combine(root,"projects");string Folder=>Path.Combine(Projects,project.id);string BaseBlend=>Path.Combine(Folder,"base.blend");string SessionBlend=>Path.Combine(Folder,"session.blend");
 readonly string[] args;StudioProject project=new();JsonObject manifest=new();bool initialized,busy;string message="Import a .blend model to begin.",error="",previewUrl="",exportPath="";double progress;long revision;List<string>warnings=[];string blender="";CancellationTokenSource? operation;Process? worker;
 readonly JsonSerializerOptions json=new(){WriteIndented=true};
 [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessageW(IntPtr h,int msg,IntPtr w,IntPtr l);
 public Microsoft.Web.WebView2.Wpf.WebView2 StudioBrowser=>Web;
 public Window? HostWindow {get;private set;}
 Window DialogOwner=>HostWindow??this;
 public TaskCompletionSource<int> DiagnosticCompletion {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public FrameworkElement CreateEmbeddedContent(Window owner){
  HostWindow=owner;Loaded-=Initialize;var content=(FrameworkElement)Content;Content=null;
  bool started=false;content.Loaded+=(_,_)=>{if(started)return;started=true;Initialize(this,new RoutedEventArgs());};
  owner.Closing+=(_,_)=>{operation?.Cancel();if(worker is {HasExited:false})worker.Kill(true);};
  owner.Closed+=(_,_)=>{initialized=false;Web.Dispose();Close();};return content;
 }
 public MainWindow(string[] arguments){InitializeComponent();args=arguments;Loaded+=Initialize;Closing+=(_,_)=>{operation?.Cancel();if(worker is {HasExited:false})worker.Kill(true);};}
 async void Initialize(object sender,RoutedEventArgs e){
  try {
   Directory.CreateDirectory(Projects);blender=FindBlender();
   var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"browser"),new CoreWebView2EnvironmentOptions("--disable-features=msWebOOUI"));
   await Web.EnsureCoreWebView2Async(environment);
   Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;Web.CoreWebView2.Settings.IsStatusBarEnabled=false;
   Web.CoreWebView2.SetVirtualHostNameToFolderMapping("studio.local",Path.Combine(AppContext.BaseDirectory,"ui"),CoreWebView2HostResourceAccessKind.Allow);
   Web.CoreWebView2.SetVirtualHostNameToFolderMapping("project.studio.local",Projects,CoreWebView2HostResourceAccessKind.Allow);
   Web.CoreWebView2.NavigationStarting+=(_,v)=>{if(!v.Uri.StartsWith("https://studio.local/",StringComparison.Ordinal))v.Cancel=true;};Web.CoreWebView2.NewWindowRequested+=(_,v)=>v.Handled=true;
   if(HostWindow!=null)await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('DOMContentLoaded',()=>document.body.classList.add('embedded'));" );
   Web.CoreWebView2.WebMessageReceived+=Receive;initialized=true;Web.Source=new Uri("https://studio.local/index.html");StartupMessage.Visibility=Visibility.Collapsed;
   if(args.Length>0&&args[0]=="--smoke-test")_ = SmokeTest();
   else if(args.Length>0&&File.Exists(args[0]))await Job(()=>ImportModel(args[0]));
  }catch(Exception ex){StartupMessage.Text="Emote Studio could not start.\n"+ex.Message+"\nInstall Microsoft Edge WebView2 Runtime if it is missing.";File.WriteAllText(Path.Combine(root,"startup-error.txt"),ex.ToString());DiagnosticCompletion.TrySetException(ex);}
 }
 string FindBlender(){
  string settings=Path.Combine(root,"settings.json");if(File.Exists(settings)){try{var saved=JsonNode.Parse(File.ReadAllText(settings))?["blender"]?.GetValue<string>();if(File.Exists(saved))return saved!;}catch(JsonException){}}
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");return Directory.Exists(folder)?Directory.EnumerateFiles(folder,"blender.exe",SearchOption.AllDirectories).OrderDescending().FirstOrDefault()??"":"";
 }
 void SendState(){if(!initialized)return;var state=new {version=Version,busy,progress,message,error,blender,project.projectName,model=project.model,project.emotes,project.selectedEmote,project.selectedClip,project.wheelPose,previewUrl,previewRevision=revision,warnings,mappingNeeded=manifest["mappingNeeded"]?.GetValue<bool>()??false,boneMap=manifest["boneMap"],sourceBones=manifest["sourceBones"],exportPath};Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="state",state}));}
 async void Receive(object? sender,CoreWebView2WebMessageReceivedEventArgs e){
  if(!e.Source.StartsWith("https://studio.local/",StringComparison.Ordinal))return;
  try {
   var m=JsonNode.Parse(e.WebMessageAsJson)??throw new IOException("Invalid command.");string type=m["type"]?.GetValue<string>()??"";
   if(type=="ready"){SendState();return;}if(type=="close"){DialogOwner.Close();return;}if(type=="minimize"){DialogOwner.WindowState=WindowState.Minimized;return;}if(type=="maximize"){DialogOwner.WindowState=DialogOwner.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;return;}
   if(type=="dragWindow"){ReleaseCapture();SendMessageW(new WindowInteropHelper(DialogOwner).Handle,0xA1,(IntPtr)2,IntPtr.Zero);return;}
   if(type=="cancel"){operation?.Cancel();return;}if(busy)return;
   switch(type){
    case "chooseBlender":var exe=new OpenFileDialog{Title="Choose blender.exe",Filter="Blender executable|blender.exe"};if(exe.ShowDialog(DialogOwner)==true){blender=exe.FileName;File.WriteAllText(Path.Combine(root,"settings.json"),JsonSerializer.Serialize(new{blender}));SendState();}break;
    case "chooseModel":var model=new OpenFileDialog{Title="Import a Blender model",Filter="Blender model|*.blend",Multiselect=false};if(model.ShowDialog(DialogOwner)==true)await Job(()=>ImportModel(model.FileName));break;
    case "addEmotes":RequireModel();var files=new OpenFileDialog{Title="Add one or more exported emotes",Filter="Exported emotes|*.blend;*.json|Blender emotes|*.blend|Porter animation data|*.json",Multiselect=true};if(files.ShowDialog(DialogOwner)==true)await Job(()=>AddEmotes(files.FileNames));break;
    case "selectEmote":RequireModel();project.selectedEmote=m["id"]?.GetValue<string>();project.selectedClip=m["clipIndex"]?.GetValue<int>()??0;await Job(Preview);break;
    case "removeEmote":var id=m["id"]?.GetValue<string>();project.emotes.RemoveAll(x=>x.id==id);if(project.selectedEmote==id){project.selectedEmote=project.emotes.FirstOrDefault()?.id;project.selectedClip=0;}await Job(Compose);break;
    case "selectRig":RequireModel();project.rig=m["name"]!.GetValue<string>();project.model!["rig"]=project.rig;project.boneMap.Clear();await Job(Compose);break;
    case "mapBones":RequireModel();project.boneMap=m["mapping"]?.Deserialize<Dictionary<string,string>>()??[];await Job(Compose);break;
    case "export":RequireModel();string format=m["format"]?.GetValue<string>()??"BLEND";if(!new[]{"BLEND","GLB","FBX"}.Contains(format))throw new IOException("Choose .blend, .glb or .fbx.");var save=new SaveFileDialog{Title="Save animated model",Filter=format+" file|*."+format.ToLowerInvariant(),DefaultExt=format.ToLowerInvariant(),FileName=project.projectName+"."+format.ToLowerInvariant(),OverwritePrompt=true};if(save.ShowDialog(DialogOwner)==true)await Job(()=>Export(save.FileName,format));break;
    case "setWheelPose":RequireModel();var requested=m["pose"]?.Deserialize<Dictionary<string,double>>()??[];var bones=project.model?["bones"]?.Deserialize<string[]>()??[];
      if(requested.Count>8||requested.Any(p=>!System.Text.RegularExpressions.Regex.IsMatch(p.Key,@"^wheel_(FL|FR|RL|RR)_(steer|roll)$")||!bones.Contains(p.Key)||!double.IsFinite(p.Value)))throw new IOException("Invalid wheel pose.");
      project.wheelPose=requested.ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,p.Key.EndsWith("steer")?-Math.PI/2:-Math.PI*20,p.Key.EndsWith("steer")?Math.PI/2:Math.PI*20));Persist();SendState();break;
    case "saveProject":RequireModel();var projectFile=new SaveFileDialog{Title="Save Emote Studio project",Filter="Emote Studio project|*.oozy-emotes",DefaultExt="oozy-emotes",FileName=project.projectName+".oozy-emotes",OverwritePrompt=true};if(projectFile.ShowDialog(DialogOwner)==true)await Job(()=>SaveProject(projectFile.FileName));break;
    case "openProject":var open=new OpenFileDialog{Title="Open Emote Studio project",Filter="Emote Studio project|*.oozy-emotes"};if(open.ShowDialog(DialogOwner)==true)await Job(()=>OpenProject(open.FileName));break;
   }
  }catch(Exception ex){error=ex.Message;SendState();}
 }
 void RequireModel(){if(project.model==null)throw new IOException("Import a rigged .blend model first.");}
 async Task Job(Func<Task> action){if(busy)return;busy=true;error="";operation=new();progress=0;SendState();try{await action();message="Ready";progress=1;}catch(OperationCanceledException){message="Cancelled. Your original files are unchanged.";}catch(Exception ex){error=ex.Message;message="Could not complete this operation.";File.WriteAllText(Path.Combine(root,"last-error.txt"),ex.ToString());}finally{busy=false;operation.Dispose();operation=null;SendState();}}
 async Task<JsonObject> Run(JsonObject request){
  if(!File.Exists(blender))throw new IOException("Blender was not found. Use Choose Blender once; Emote Studio runs it in the background.");
  Directory.CreateDirectory(Folder);string temp=Path.Combine(Folder,"jobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string result=Path.Combine(temp,"result.json"),input=Path.Combine(temp,"request.json");request["result"]=result;await File.WriteAllTextAsync(input,request.ToJsonString());
  var start=new ProcessStartInfo(blender){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=temp};foreach(var a in new[]{"--background","--factory-startup","--disable-autoexec","--python-exit-code","1","--python",Path.Combine(AppContext.BaseDirectory,"engine","studio_engine.py"),"--",input})start.ArgumentList.Add(a);
  using var process=Process.Start(start)??throw new IOException("Could not start Blender.");worker=process;
  using var log=new StreamWriter(Path.Combine(temp,"worker.log"));async Task Read(StreamReader reader){while(await reader.ReadLineAsync() is {} line){lock(log){log.WriteLine(line);log.Flush();}if(line.StartsWith("STUDIO_PROGRESS ",StringComparison.Ordinal)){try{var value=JsonNode.Parse(line[16..]);await Dispatcher.InvokeAsync(()=>{message=value?["message"]?.GetValue<string>()??message;progress=value?["progress"]?.GetValue<double>()??progress;SendState();});}catch(JsonException){}}}}
  var stdout=Read(process.StandardOutput);var stderr=Read(process.StandardError);try{await process.WaitForExitAsync(operation?.Token??CancellationToken.None);}catch{if(!process.HasExited)process.Kill(true);await Task.WhenAll(stdout,stderr);throw;}finally{worker=null;}
  await Task.WhenAll(stdout,stderr);if(!File.Exists(result))throw new IOException("Blender did not finish. Details are in "+Path.Combine(temp,"worker.log"));var document=JsonNode.Parse(await File.ReadAllTextAsync(result))!.AsObject();if(document["error"]!=null||process.ExitCode!=0)throw new IOException(document["error"]?.GetValue<string>()??"Blender could not complete the operation.");return document;
 }
 async Task ImportModel(string path){
  var previous=project;var previousManifest=manifest;var previousWarnings=warnings;var previousPreview=previewUrl;project=new(){projectName=Path.GetFileNameWithoutExtension(path)};Directory.CreateDirectory(Folder);manifest=new();warnings=[];
  try{string preview=Path.Combine(Folder,"model.glb");var result=await Run(new(){["action"]="inspectModel",["source"]=path,["baseOutput"]=BaseBlend,["previewOutput"]=preview});project.model=result["model"]!.AsObject();project.rig=project.model["rig"]!.GetValue<string>();warnings=result["warnings"]?.Deserialize<List<string>>()??[];File.Copy(BaseBlend,SessionBlend,true);SetPreview(preview);Persist();}catch{project=previous;manifest=previousManifest;warnings=previousWarnings;previewUrl=previousPreview;throw;}
 }
 async Task AddEmotes(string[] paths){
  foreach(var path in paths){operation!.Token.ThrowIfCancellationRequested();if(project.emotes.Any(x=>Path.GetFullPath(x.path).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)))continue;var item=new EmoteInfo{path=path};string payload=Path.Combine(Folder,item.id+".json");try{var result=await Run(new(){["action"]="inspectEmote",["source"]=path,["payloadOutput"]=payload});item.name=result["name"]!.GetValue<string>();item.clips=result["clips"]!.Deserialize<List<ClipInfo>>()!;item.payload=Path.GetFileName(payload);project.emotes.Add(item);}catch(IOException ex){warnings.Add(Path.GetFileName(path)+": "+ex.Message);}}
  if(project.selectedEmote==null)project.selectedEmote=project.emotes.FirstOrDefault()?.id;await Compose();
 }
 async Task Compose(){
  RequireModel();if(project.emotes.Count==0){File.Copy(BaseBlend,SessionBlend,true);manifest=new(){["rig"]=project.rig,["actions"]=new JsonArray()};Persist();await Preview();return;}
  var source=new JsonArray();foreach(var e in project.emotes)source.Add(new JsonObject{["id"]=e.id,["name"]=e.name,["payload"]=Path.Combine(Folder,e.payload),["clips"]=JsonSerializer.SerializeToNode(e.clips)});
  string output=Path.Combine(Folder,"session-next.blend");var result=await Run(new(){["action"]="compose",["base"]=BaseBlend,["emotes"]=source,["rig"]=project.rig,["selectedEmote"]=project.selectedEmote,["clipIndex"]=project.selectedClip,["boneMap"]=JsonSerializer.SerializeToNode(project.boneMap),["blendOutput"]=output});File.Move(output,SessionBlend,true);manifest=result;
  foreach(var a in result["actions"]!.AsArray()) {
   var e=project.emotes.First(x=>x.id==a!["emoteId"]!.GetValue<string>());int index=a!["clipIndex"]!.GetValue<int>();
   e.clips[index].frames=a["frames"]!.GetValue<int>();e.clips[index].fps=a["fps"]!.GetValue<double>();e.clips[index].duration=a["duration"]!.GetValue<double>();e.clips[index].loop=a["loop"]!.GetValue<bool>();
  }
  warnings=result["warnings"]?.Deserialize<List<string>>()??[];project.model!["bones"]=result["targetBones"]?.DeepClone()??project.model["bones"]?.DeepClone();
  string previewFolder=Path.Combine(Folder,"previews");if(Directory.Exists(previewFolder))foreach(string file in Directory.GetFiles(previewFolder,"*.glb"))File.Delete(file);Persist();await Preview();
 }
 string PreviewAddress(string path)=>"https://project.studio.local/"+string.Join('/',Path.GetRelativePath(Projects,path).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
 void SetPreview(string path){previewUrl=PreviewAddress(path);revision++;SendState();}
 async Task Preview(){
  RequireModel();if(project.selectedEmote!=null && !project.emotes.Any(x=>x.id==project.selectedEmote))throw new IOException("Choose an emote from the library.");
  var actions=manifest["actions"]?.AsArray();bool valid=project.selectedEmote==null||actions?.Any(a=>a?["emoteId"]?.GetValue<string>()==project.selectedEmote && a?["clipIndex"]?.GetValue<int>()==project.selectedClip)==true;
  if(!valid){previewUrl=PreviewAddress(Path.Combine(Folder,"model.glb"));revision++;Persist();return;}
  string folder=Path.Combine(Folder,"previews");Directory.CreateDirectory(folder);string output=Path.Combine(folder,(project.selectedEmote??"model")+"-"+project.selectedClip+".glb");
  if(!File.Exists(output)){var result=await Run(new(){["action"]="preview",["blend"]=SessionBlend,["rig"]=project.rig,["selectedEmote"]=project.selectedEmote,["clipIndex"]=project.selectedClip,["output"]=output});project.model!["bones"]=result["targetBones"]?.DeepClone()??project.model["bones"]?.DeepClone();}SetPreview(output);Persist();
 }
 async Task Export(string path,string format){
  if(project.selectedEmote!=null && manifest["actions"]?.AsArray().Any(a=>a?["emoteId"]?.GetValue<string>()==project.selectedEmote&&a?["clipIndex"]?.GetValue<int>()==project.selectedClip)!=true)throw new IOException("Map the model's bones before saving this emote.");
  string directory=Path.Combine(Folder,"exports",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string staging=Path.Combine(directory,"export."+format.ToLowerInvariant());
  await Run(new(){["action"]="export",["blend"]=SessionBlend,["rig"]=project.rig,["selectedEmote"]=project.selectedEmote,["clipIndex"]=project.selectedClip,["format"]=format,["wheelPose"]=JsonSerializer.SerializeToNode(project.wheelPose),["output"]=staging});File.Move(staging,path,true);exportPath=path;Persist();
 }
 void Persist(){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"project.json"),JsonSerializer.Serialize(project,json));File.WriteAllText(Path.Combine(Folder,"manifest.json"),manifest.ToJsonString());}
 async Task SaveProject(string path){
  Persist();string folder=Folder;var files=new[]{"base.blend","session.blend","model.glb","project.json","manifest.json"}.Concat(project.emotes.Select(e=>e.payload)).Distinct().ToArray();string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{await Task.Run(()=>{using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){foreach(var file in files){operation?.Token.ThrowIfCancellationRequested();zip.CreateEntryFromFile(Path.Combine(folder,file),file,CompressionLevel.Fastest);}}operation?.Token.ThrowIfCancellationRequested();File.Move(temp,path,true);});exportPath=path;}finally{if(File.Exists(temp))File.Delete(temp);}
 }

 async Task OpenProject(string path){
  var previous=project;var previousManifest=manifest;var previousWarnings=warnings;var previousPreview=previewUrl;project=new();Directory.CreateDirectory(Folder);try{using(var zip=ZipFile.OpenRead(path)){long total=0;if(zip.Entries.Count>2000)throw new IOException("This project contains too many files.");foreach(var e in zip.Entries){total+=e.Length;if(total>4L*1024*1024*1024)throw new IOException("This project is too large.");if(e.FullName!=Path.GetFileName(e.FullName)||e.FullName.Contains(':'))throw new IOException("Invalid project entry.");e.ExtractToFile(Path.Combine(Folder,e.FullName),true);}}
   string id=project.id;var loaded=JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(Path.Combine(Folder,"project.json")))??throw new IOException("Invalid project.");if(loaded.schema!=1)throw new IOException("Unsupported project version.");loaded.id=id;project=loaded;if(project.emotes.Any(e=>!Guid.TryParseExact(e.id,"N",out _)||string.IsNullOrWhiteSpace(e.payload)||e.payload!=Path.GetFileName(e.payload)||e.payload.Contains(':')))throw new IOException("Invalid emote payload path.");manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(Folder,"manifest.json")))!.AsObject();warnings=manifest["warnings"]?.Deserialize<List<string>>()??[];await Preview();Persist();}catch{project=previous;manifest=previousManifest;warnings=previousWarnings;previewUrl=previousPreview;throw;}
 }
 async Task SmokeTest(){
  string output=args[1];Directory.CreateDirectory(output);foreach(string stale in new[]{"error.txt","result.json","studio.png"}){string file=Path.Combine(output,stale);if(File.Exists(file))File.Delete(file);}try{
   for(int i=0;i<100;i++){await Task.Delay(100);if(await Web.CoreWebView2.ExecuteScriptAsync("document.readyState") is "\"complete\"")break;}
   await Job(()=>ImportModel(args[2]));if(error!="")throw new Exception(error);
   await Job(()=>AddEmotes(args.Skip(3).ToArray()));if(error!="")throw new Exception(error);
   foreach(var e in project.emotes){project.selectedEmote=e.id;project.selectedClip=0;await Job(Preview);if(error!="")throw new Exception(error);}
   foreach(string format in new[]{"BLEND","GLB","FBX"}){await Job(()=>Export(Path.Combine(output,"studio-export."+format.ToLowerInvariant()),format));if(error!="")throw new Exception(error);}
   await SaveProject(Path.Combine(output,"studio-project.oozy-emotes"));await OpenProject(Path.Combine(output,"studio-project.oozy-emotes"));SendState();
   bool loaded=false;for(int i=0;i<150;i++){await Task.Delay(200);string value=await Web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.EmoteStudioPreview||{})");if(JsonNode.Parse(JsonSerializer.Deserialize<string>(value)??"{}")?["loaded"]?.GetValue<bool>()==true){loaded=true;break;}}
   if(!loaded)throw new Exception("3D preview did not load in WebView2.");
   await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('stop').click();document.getElementById('seek').value='15';document.getElementById('seek').dispatchEvent(new Event('input',{bubbles:true}));");
   await Task.Delay(100);
   string previewState=JsonSerializer.Deserialize<string>(await Web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.EmoteStudioPreview)"))??"{}";
   if(JsonNode.Parse(previewState)?["frame"]?.GetValue<int>()!=15)throw new Exception("Timeline seek did not select frame 15.");
   await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('step-forward').click();");
   await Task.Delay(100);
   if(JsonNode.Parse(JsonSerializer.Deserialize<string>(await Web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.EmoteStudioPreview)"))??"{}")?["frame"]?.GetValue<int>()!=16)throw new Exception("Frame stepping failed.");
   var wheelState=JsonNode.Parse(previewState);
   if(wheelState?["procedural"]?.GetValue<bool>()==true){
    if(wheelState["proceduralMeshes"]?.GetValue<int>()!=8)throw new Exception("Expected eight animated wheel faces.");
    await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('wheel-corner').value='FL';document.getElementById('wheel-corner').dispatchEvent(new Event('change'));document.getElementById('wheel-mode').value='steer';document.getElementById('wheel-mode').dispatchEvent(new Event('change'));document.getElementById('wheel-angle').value='30';document.getElementById('wheel-angle').dispatchEvent(new Event('input'));");
    await Task.Delay(500);
    if(!project.wheelPose.TryGetValue("wheel_FL_steer",out double angle)||Math.Abs(angle-Math.PI/6)>.001)throw new Exception("Wheel pose did not persist.");
    await Job(()=>Export(Path.Combine(output,"posed-wheel.blend"),"BLEND"));if(error!="")throw new Exception(error);
    await SaveProject(Path.Combine(output,"posed-project.oozy-emotes"));await OpenProject(Path.Combine(output,"posed-project.oozy-emotes"));SendState();await Task.Delay(1500);
    if(!project.wheelPose.TryGetValue("wheel_FL_steer",out angle)||Math.Abs(angle-Math.PI/6)>.001)throw new Exception("Wheel pose did not reopen.");
   }
   await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('play').click();");await Task.Delay(400);
   using(var shot=File.Create(Path.Combine(output,"studio.png")))await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
   File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Success=true,Project=project,Manifest=manifest,Preview=previewUrl}));Environment.ExitCode=0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());Environment.ExitCode=1;}finally{DiagnosticCompletion.TrySetResult(Environment.ExitCode);if(HostWindow==null)Close();}
 }
}
