using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
namespace FortniteObj;
public partial class MainWindow {
 private EmoteStudio.MainWindow? _studioEditor;
 private bool _studioSelected;
 private void PorterTab_Click(object sender,RoutedEventArgs e)=>SelectTool(false);
 private void StudioTab_Click(object sender,RoutedEventArgs e)=>SelectTool(true);
 private void SelectTool(bool studio,string[]? diagnosticArgs=null){
  if(studio&&_studioEditor==null){
   _studioEditor=new EmoteStudio.MainWindow(diagnosticArgs??Array.Empty<string>());
   StudioHost.Content=_studioEditor.CreateEmbeddedContent(this);
  }
  _studioSelected=studio;
  PorterWorkspace.Visibility=studio?Visibility.Collapsed:Visibility.Visible;
  PorterFooter.Visibility=studio?Visibility.Collapsed:Visibility.Visible;
  StudioHost.Visibility=studio?Visibility.Visible:Visibility.Collapsed;
  PorterToolTab.IsChecked=!studio;StudioToolTab.IsChecked=studio;
  AutomationProperties.SetHelpText(PorterToolTab,studio?"Switch to asset exporting":"Asset exporting is selected");
  AutomationProperties.SetHelpText(StudioToolTab,studio?"Animation editing is selected":"Switch to animation editing");
 }
 internal async Task RunMergedDiagnostics(string output,string? model,string[] emotes){
  Directory.CreateDirectory(output);
  void SaveWindow(string name){UpdateLayout();var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(Path.Combine(output,name));png.Save(f);}
  SelectTool(false);await Task.Delay(500);SaveWindow("porter.png");
  var studioArgs=model==null?Array.Empty<string>():new[]{"--smoke-test",output,model}.Concat(emotes).ToArray();
  SelectTool(true,studioArgs);
  if(model!=null){int code=await _studioEditor!.DiagnosticCompletion.Task.WaitAsync(TimeSpan.FromMinutes(12));if(code!=0)throw new IOException("Studio smoke failed: "+File.ReadAllText(Path.Combine(output,"error.txt")));}
  else {for(int i=0;i<200&&_studioEditor!.StudioBrowser.CoreWebView2==null;i++)await Task.Delay(100);await Task.Delay(800);}
  var web=_studioEditor!.StudioBrowser.CoreWebView2??throw new IOException("Studio browser did not initialize.");
  string before=await web.ExecuteScriptAsync("JSON.stringify(window.EmoteStudioPreview||{})");
  SelectTool(false);await Task.Delay(150);SelectTool(true);await Task.Delay(200);
  if(StudioHost.Content==null||_studioEditor.StudioBrowser.CoreWebView2!=web)throw new IOException("Switching tabs recreated the editor.");
  string mode=await web.ExecuteScriptAsync("document.body.classList.contains('embedded')");if(mode!="true")throw new IOException("Duplicate Studio header was not hidden.");
  async Task Capture(string name){
   UpdateLayout();var shell=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);shell.Render(this);
   using var buffer=new MemoryStream();await web.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,buffer);buffer.Position=0;
   var editor=new BitmapImage();editor.BeginInit();editor.CacheOption=BitmapCacheOption.OnLoad;editor.StreamSource=buffer;editor.EndInit();editor.Freeze();
   var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){draw.DrawImage(shell,new Rect(0,0,ActualWidth,ActualHeight));var bounds=_studioEditor.StudioBrowser.TranslatePoint(new Point(0,0),this);draw.DrawImage(editor,new Rect(bounds.X,bounds.Y,_studioEditor.StudioBrowser.ActualWidth,_studioEditor.StudioBrowser.ActualHeight));}
   var composed=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);composed.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(composed));using var f=File.Create(Path.Combine(output,name));png.Save(f);
  }
  await Capture("studio.png");
  Width=MinWidth;Height=MinHeight;await Task.Delay(500);await Capture("minimum-studio.png");SelectTool(false);await Task.Delay(100);SaveWindow("minimum-porter.png");SelectTool(true);
  File.WriteAllText(Path.Combine(output,"tabs.json"),System.Text.Json.JsonSerializer.Serialize(new{SingleWindow=true,EmbeddedMode=true,StateRetained=true,PreviewBefore=before,TabCenter=(PorterToolTab.TranslatePoint(new Point(0,0),this).X+StudioToolTab.TranslatePoint(new Point(StudioToolTab.ActualWidth,0),this).X)/2,Width=ActualWidth}));
 }
}
