using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using FortniteObj.Core;
namespace FortniteObj;
public partial class MainWindow
{
 private CancellationTokenSource? _skinPreviewCancellation;
 private readonly SemaphoreSlim _skinPreviewGate=new(1,1);
 private long _skinPreviewRevision;
 private long _styleRefreshRevision;
 private bool _closingAfterPreview;
 private void CancelSkinPreview(){++_skinPreviewRevision;_skinPreviewCancellation?.Cancel();}
 internal async Task StopSkinPreviewAsync(){CancelSkinPreview();await _skinPreviewGate.WaitAsync();_skinPreviewGate.Release();}
 private void QueueSkinPreview()
 {
  CancelSkinPreview();
  if(!IsFortnite||_scanning||_operation!=null||AssetOverlay.Visibility!=Visibility.Visible||OverlayThumbnail.Asset is not {} asset||asset.Category!="Skins")
  {SkinPreviewPanel.Visibility=Visibility.Collapsed;return;}
  SkinPreviewPanel.Visibility=Visibility.Visible;
  SkinPreviewStatus.Text="Updating selected appearance…";SkinPreviewStatus.ToolTip="The first preview may take a little while. Returning to a rendered combination uses its cached image.";SkinPreviewRetry.Visibility=Visibility.Collapsed;
  OverlayThumbnail.SetPreviewState("Updating selected appearance…");
  string selection=_selectedStyles.GetValueOrDefault(asset.Id,"Default");var settings=new AppSettings{GameFolder=GamePath.Text,MappingFile=MappingPath.Text,EngineVersion=EngineVersion.SelectedItem?.ToString()??"",MatteMaterials=MatteMaterials.IsChecked==true};
  var cancellation=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_skinPreviewCancellation=cancellation;
  long revision=_skinPreviewRevision;
  _=UpdateSkinPreview(asset,selection,settings,revision,cancellation);
 }
 private async Task UpdateSkinPreview(Cosmetic asset,string selection,AppSettings settings,long revision,CancellationTokenSource cancellation)
 {
  bool current()=>revision==_skinPreviewRevision&&!cancellation.IsCancellationRequested&&AssetOverlay.Visibility==Visibility.Visible&&OverlayThumbnail.Asset?.Id==asset.Id&&IsFortnite;
  bool entered=false;
  try
  {
   await Task.Delay(400,cancellation.Token);
   await _skinPreviewGate.WaitAsync(cancellation.Token);entered=true;
   if(!current())return;
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);timeout.CancelAfter(TimeSpan.FromMinutes(6));
   var progress=new Progress<string>(message=>{if(current())SkinPreviewStatus.Text=message;});
   var result=await SkinCustomizationRenderer.Render(_session,asset,selection,settings,progress,timeout.Token);
   if(!current())return;
   var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(result.Path);image.EndInit();image.Freeze();
   OverlayThumbnail.SetPreview(image);SkinPreviewStatus.Text="Selected appearance · updates automatically"+(result.Warnings.Length>0?$" · {result.Warnings.Length} export warning(s)":"");
   SkinPreviewStatus.ToolTip=result.Warnings.Length>0?string.Join("\n",result.Warnings):"Rendered from your complete selection using the export materials. Unreal-specific effects may be simplified.";
  }
  catch(OperationCanceledException){if(current()){OverlayThumbnail.SetPreviewState("Preview timed out");SkinPreviewStatus.Text="Preview took too long. Try again.";SkinPreviewRetry.Visibility=Visibility.Visible;}}
  catch(Exception ex){if(current()){OverlayThumbnail.SetPreviewState("Preview unavailable");SkinPreviewStatus.Text=ex.Message;SkinPreviewRetry.Visibility=Visibility.Visible;Report("Customization preview: "+ex.Message);}}
  finally{if(entered){_session.ResetPreviewCancellation();_skinPreviewGate.Release();}if(ReferenceEquals(_skinPreviewCancellation,cancellation))_skinPreviewCancellation=null;cancellation.Dispose();}
 }
 private async Task CloseAfterSkinPreview(){await StopSkinPreviewAsync();Close();}
 private void SkinPreviewRetry_Click(object sender,RoutedEventArgs e)=>QueueSkinPreview();
}
