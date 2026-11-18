using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FortniteObj.Core;
namespace FortniteObj;
public partial class MainWindow {
 internal async Task CheckCardGallery(string output){
  Directory.CreateDirectory(output);
  void Save(string name){UpdateLayout();var image=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(output,name));png.Save(file);}
  using var resource=typeof(WebsiteAssetId).Assembly.GetManifestResourceStream("website-id-registry.json")!;using var registry=JsonDocument.Parse(resource);
  List<Cosmetic> Entries(string game)=>registry.RootElement.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("game").GetString()==game).Select(a=>new Cosmetic(a.GetProperty("assetId").GetString()!,a.GetProperty("name").GetString()!,a.GetProperty("category").GetString()!,"",ImageUrl:a.TryGetProperty("image",out var v)&&!string.IsNullOrWhiteSpace(v.GetString())?new Uri(new Uri("https://oozycloud.github.io/oozys-Fortnite-Porter/"),v.GetString()!).AbsoluteUri:null)).ToList();
  GameSelector.SelectedIndex=0;
  _cosmetics=Entries("Fortnite");_gameCatalogs[0]=_cosmetics;RefreshFilter();await Task.Delay(800);
  var panel=Descendants<VirtualizingCardPanel>(CosmeticsGrid).FirstOrDefault()??throw new Exception("Gallery is not virtualized.");
  int realized=Descendants<ListBoxItem>(CosmeticsGrid).Count();if(realized>150)throw new Exception("The whole catalogue was realized.");
  for(int i=0;i<200&&!Descendants<AssetThumbnail>(CosmeticsGrid).Any(t=>t.Source!=null);i++)await Task.Delay(100);
  await Task.Delay(800);Save("gallery.png");
  var first=_cosmetics.First();CosmeticsGrid.SelectedItem=first;OpenAssetOverlay(first);await Task.Delay(300);if(PorterWorkspace.IsEnabled||AssetOverlay.Visibility!=Visibility.Visible)throw new Exception("Overlay did not isolate library input.");Save("export-overlay.png");
  var oldFormat=ExportFormat.SelectedIndex;OverlayExportFormat.SelectedIndex=2;if(ExportFormat.SelectedIndex!=2)throw new Exception("Overlay export format is not connected.");ExportFormat.SelectedIndex=oldFormat;
  CloseAssetOverlay();if(!PorterWorkspace.IsEnabled||AssetOverlay.Visibility!=Visibility.Collapsed)throw new Exception("Overlay did not close.");
  CosmeticsGrid.SelectedItems.Add(_cosmetics[1]);if(CosmeticsGrid.SelectedItems.Count!=2||ReviewSelectionButton.Visibility!=Visibility.Visible)throw new Exception("Batch card selection failed.");ReviewSelection_Click(this,new RoutedEventArgs());if(!OverlayAssetName.Text.StartsWith("2"))throw new Exception("Batch review did not retain selections.");CloseAssetOverlay();
  SceneMode.IsChecked=true;if(SceneMode.IsChecked==true||LegacySceneControls.IsVisible)throw new Exception("Combined mode is still available.");
  panel.SetVerticalOffset(12000);await Task.Delay(250);if(Descendants<ListBoxItem>(CosmeticsGrid).Count()>150)throw new Exception("Scrolling stopped virtualizing.");
  Search.Text="Renegade";await Task.Delay(300);if(CosmeticsGrid.Items.Count==0)throw new Exception("Card search did not filter.");Search.Text="";
  GameSelector.SelectedIndex=1;_cosmetics=Entries("PC Building Simulator 2");_gameCatalogs[1]=_cosmetics;RefreshFilter();await Task.Delay(200);
  var motherboard=_cosmetics.First(a=>a.Category=="Motherboards"&&a.Name.Contains("Taichi",StringComparison.OrdinalIgnoreCase));CosmeticsGrid.SelectedItem=motherboard;CosmeticsGrid.ScrollIntoView(motherboard);await Task.Delay(300);
  if(await AssetArtwork.Get(motherboard)==null)throw new Exception("PC part artwork did not load.");File.WriteAllText(Path.Combine(output,"panel-state.json"),JsonSerializer.Serialize(Descendants<VirtualizingCardPanel>(CosmeticsGrid).Select(p=>new{p.VerticalOffset,p.ViewportHeight,p.ExtentHeight,p.ActualHeight,Children=Descendants<ListBoxItem>(p).Select(c=>new{Name=(c.DataContext as Cosmetic)?.Name,Y=c.TranslatePoint(new Point(),CosmeticsGrid).Y,c.ActualHeight}).ToArray()})));if(Descendants<ListBoxItem>(CosmeticsGrid).Count(c=>{double y=c.TranslatePoint(new Point(),CosmeticsGrid).Y;return y>=0&&y<CosmeticsGrid.ActualHeight;})<3)throw new Exception("Scrolling left cards outside the viewport.");Save("pc-parts-gallery.png");OpenAssetOverlay(motherboard);await Task.Delay(300);if(!ExportButton.IsEnabled)throw new Exception($"PC export disabled: selected={CosmeticsGrid.SelectedItems.Count}, can={CanExport()}, busy={_operation!=null}, enabledLocal={ExportButton.ReadLocalValue(IsEnabledProperty)}, inspector={InspectorShell.IsEnabled}, overlay={AssetOverlay.IsEnabled}, scene={SceneMode.IsChecked}");Save("pc-part-overlay.png");
  Width=MinWidth;Height=MinHeight;await Task.Delay(400);Save("minimum-overlay.png");CloseAssetOverlay();await Task.Delay(200);Save("minimum-gallery.png");
  var rl=new Cosmetic("RL1580","Zomba","Wheels",@"C:\Program Files\Epic Games\rocketleague\TAGame\CookedPCConsole\WHEEL_Proto09_SF.upk",PackagePath:@"C:\Program Files\Epic Games\rocketleague\TAGame\CookedPCConsole");
  bool nativeImage=false;if(File.Exists(rl.ObjectPath)){nativeImage=await AssetArtwork.Get(rl)!=null;if(!nativeImage)throw new Exception("Native Rocket League thumbnail did not load.");}
  File.WriteAllText(Path.Combine(output,"cards-result.json"),JsonSerializer.Serialize(new{Virtualized=true,RealizedContainers=realized,RegistryAssets=registry.RootElement.GetProperty("assets").GetArrayLength(),Overlay=true,BatchSelection=true,FormatBinding=true,CombinedModeRemoved=true,NativeRocketLeagueImage=nativeImage}));
 }
 static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T typed)yield return typed;foreach(var nested in Descendants<T>(child))yield return nested;}}
}
