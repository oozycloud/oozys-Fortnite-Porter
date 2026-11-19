using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FortniteObj.Core;
namespace FortniteObj;
public partial class MainWindow {
 IInputElement? _overlayReturnFocus;
 private void UpdateCardSelection(Cosmetic? asset,int count){
  VehicleTemplates.Visibility=asset?.Category is "Vehicles" or "Cars"?Visibility.Visible:Visibility.Collapsed;
  ReviewSelectionButton.Visibility=count>0?Visibility.Visible:Visibility.Collapsed;
  ReviewSelectionButton.Content=count>1?$"Review {count:N0} assets →":"Review asset →";
  ModeHint.Text=count>1?"Choose an output format and export the selected assets.":"Choose a style and output format, then export your model.";
  OverlayThumbnail.Asset=asset;OverlayAssetName.Text=asset?.Name??"No asset selected";OverlayAssetId.Text=asset?.Id??"";
  if(count>1)OverlayAssetName.Text=$"{count:N0} assets selected";
  if(asset==null)CloseAssetOverlay();
 }
 private void AssetCard_MouseUp(object sender,MouseButtonEventArgs e){if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))return;if(sender is ListBoxItem item&&item.DataContext is Cosmetic asset){if(!item.IsSelected)item.IsSelected=true;OpenAssetOverlay(asset);}}
 private void ReviewSelection_Click(object sender,RoutedEventArgs e){if(CosmeticsGrid.SelectedItems.OfType<Cosmetic>().LastOrDefault() is {} asset)OpenAssetOverlay(asset);}
 private void OpenAssetOverlay(Cosmetic asset){
  if(AssetOverlay.Visibility!=Visibility.Visible)_overlayReturnFocus=Keyboard.FocusedElement;
  OverlayThumbnail.Asset=asset;OverlayAssetName.Text=CosmeticsGrid.SelectedItems.Count>1?$"{CosmeticsGrid.SelectedItems.Count:N0} assets selected":asset.Name;OverlayAssetId.Text=asset.Id;
  OverlayThumbnail.AllowRender=true;AssetOverlay.Visibility=Visibility.Visible;PorterWorkspace.IsEnabled=false;
  Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>CloseOverlayButton.Focus()));
  UiMotion.Reveal(InspectorShell,0,8);
 }
 private void CloseAssetOverlay_Click(object sender,RoutedEventArgs e)=>CloseAssetOverlay();
 private void OverlayBackdrop_MouseDown(object sender,MouseButtonEventArgs e){if(ReferenceEquals(sender,e.OriginalSource))CloseAssetOverlay();}
 private void CloseAssetOverlay(){if(AssetOverlay==null||PorterWorkspace==null)return;bool visible=AssetOverlay.Visibility==Visibility.Visible;AssetOverlay.Visibility=Visibility.Collapsed;PorterWorkspace.IsEnabled=true;if(visible){foreach(var thumbnail in Descendants<AssetThumbnail>(CosmeticsGrid).Where(t=>t.Source==null))thumbnail.Refresh();if(_overlayReturnFocus!=null)Keyboard.Focus(_overlayReturnFocus);}}
 private void GalleryKeys(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&AssetOverlay.Visibility==Visibility.Visible){if(Descendants<ComboBox>(AssetOverlay).Any(c=>c.IsDropDownOpen))return;CloseAssetOverlay();e.Handled=true;}else if(e.Key==Key.Enter&&CosmeticsGrid.IsKeyboardFocusWithin&&CosmeticsGrid.SelectedItem is Cosmetic asset){OpenAssetOverlay(asset);e.Handled=true;}}
}
