using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FortniteObj.Core;
namespace FortniteObj;
public partial class MainWindow
{
 internal async Task CheckCustomizationPreview(string output,string name)
 {
  void Save(string file){UpdateLayout();var image=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(output,file));encoder.Save(stream);}
  async Task Ready(){var timer=Stopwatch.StartNew();while(!SkinPreviewStatus.Text.StartsWith("Selected appearance ·",StringComparison.Ordinal)){if(SkinPreviewRetry.Visibility==Visibility.Visible)throw new IOException(SkinPreviewStatus.Text);if(timer.Elapsed>TimeSpan.FromMinutes(7))throw new TimeoutException(SkinPreviewStatus.Text);await Task.Delay(100);}}
  string Hash()=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(SkinCustomizationRenderer.Root,SkinCustomizationRenderer.CacheKey(OverlayThumbnail.Asset!,_selectedStyles.GetValueOrDefault(OverlayThumbnail.Asset!.Id,"Default"),ReadSettings()))+".png")));
  GameSelector.SelectedIndex=0;_scanning=true;_scanFortnite=true;
  var progress=new DirectProgress(m=>File.AppendAllText(Path.Combine(output,"scan.log"),m+"\n"));
  try{_cosmetics=await _session.Scan(ReadSettings(),progress,_lifetime.Token);_gameCatalogs[0]=_cosmetics;}
  finally{_scanning=false;}
  RefreshFilter();var asset=_cosmetics.First(a=>a.Category=="Skins"&&a.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
  CosmeticsGrid.SelectedItem=asset;OpenAssetOverlay(asset);await Task.Delay(350);Save("loading.png");if(!InspectorPanel.IsEnabled||!CloseOverlayButton.IsEnabled)throw new Exception("Preview blocked customization or close controls.");await Ready();Save("default.png");string first=Hash();
  var combos=StyleChannelsTab.Items.OfType<TabItem>().SelectMany(tab=>Descendants<ComboBox>((DependencyObject)tab.Content)).ToArray();
  var colour=combos.FirstOrDefault(c=>c.Items.OfType<CosmeticStyleOption>().Any(o=>o.Name.Contains("Blue",StringComparison.OrdinalIgnoreCase)))??combos.First(c=>c.Items.Count>1);
  int old=colour.SelectedIndex;colour.SelectedIndex=Enumerable.Range(0,colour.Items.Count).First(i=>i!=old);
  if(OverlayThumbnail.Source!=null)throw new Exception("Old or base thumbnail remained visible after a style change.");
  await Ready();Save("changed.png");string second=Hash();if(first==second)throw new Exception("Rendered customization did not change.");
  colour.SelectedIndex=old;await Ready();if(Hash()!=first)throw new Exception("Returning to a style did not reuse its correct preview.");
  // Rapid changes must leave only the final selection on screen.
  colour.SelectedIndex=(old+1)%colour.Items.Count;colour.SelectedIndex=old;await Ready();if(Hash()!=first)throw new Exception("Rapid changes showed a stale render.");
  var ordered=new Dictionary<string,string>{{"body","1"},{"colour","2"}};var reversed=new Dictionary<string,string>{{"colour","2"},{"body","1"}};
  if(SkinCustomizationRenderer.CacheKey(asset,StyleSelection.Encode(ordered),ReadSettings())!=SkinCustomizationRenderer.CacheKey(asset,StyleSelection.Encode(reversed),ReadSettings()))throw new Exception("Equivalent combinations do not share a canonical cache key.");
  Width=MinWidth;Height=MinHeight;await Task.Delay(200);Save("minimum.png");
  colour.SelectedIndex=(old+1)%colour.Items.Count;CloseAssetOverlay();await StopSkinPreviewAsync();if(AssetOverlay.Visibility!=Visibility.Collapsed)throw new Exception("Closing did not stop preview.");
  CosmeticsGrid.SelectedItems.Add(_cosmetics.First(a=>a.Category=="Skins"&&a.Id!=asset.Id));OpenAssetOverlay(asset);await Ready();if(CosmeticsGrid.SelectedItems.Count<2)throw new Exception("Preview lost batch selection.");CloseAssetOverlay();await StopSkinPreviewAsync();
  File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Asset=asset.Name,DefaultHash=first,ChangedHash=second,ActualMountedExport=true,ChangesUpdateAutomatically=true,RapidChanges=true,CacheReuse=true,CanonicalCombination=true,CloseCancels=true,BatchPreserved=true}));
 }
}
