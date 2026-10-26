using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FortniteObj.Core;
namespace FortniteObj;
public partial class MainWindow
{
 private Dictionary<string,RocketAppearance> _rocketAppearances=new();
 private Image? _rocketPreviewImage;
 private TextBlock? _rocketPreviewStatus;
 private void BuildRocketAppearance(Cosmetic? asset)
 {
  RocketAppearancePanel.Children.Clear();RocketAppearancePanel.Visibility=asset==null?Visibility.Collapsed:Visibility.Visible;
  if(asset==null)return;
  if(!_rocketAppearances.TryGetValue(asset.Id,out var appearance))_rocketAppearances[asset.Id]=appearance=new();
  void changed(){if(_updatingStyles)return;SaveAppearanceSettings();if(_rocketPreviewImage!=null)_rocketPreviewImage.Source=null;if(_rocketPreviewStatus!=null)_rocketPreviewStatus.Text="Selections changed. Generate a preview to see this export.";}
  void label(string text)=>RocketAppearancePanel.Children.Add(new TextBlock{Text=text,Foreground=(Brush)FindResource("Muted"),FontSize=11,Margin=new Thickness(0,12,0,5)});
  ComboBox combo(string title,IEnumerable<string> values,string selected,Action<string> update){label(title);var c=new ComboBox{ItemsSource=values.ToArray(),SelectedItem=selected,MinWidth=160};c.SelectionChanged+=(_,_)=>{if(c.SelectedItem is string value){update(value);changed();}};RocketAppearancePanel.Children.Add(c);return c;}
  void color(string title,string selected,Action<string> update){
   label(title);
   var presets=new[]{new CosmeticStyleOption("#195CB8","Blue","#195CB8"),new("#EF8124","Orange","#EF8124"),new("#B7162D","Crimson","#B7162D"),new("#264DD9","Cobalt","#264DD9"),new("#63CBF2","Sky Blue","#63CBF2"),new("#208448","Forest Green","#208448"),new("#A2D928","Lime","#A2D928"),new("#7C3DB8","Purple","#7C3DB8"),new("#E881B5","Pink","#E881B5"),new("#ECEEF0","Titanium White","#ECEEF0"),new("#15171B","Black","#15171B"),new("#E1CB29","Saffron","#E1CB29")};
   var choices=new ComboBox{ItemsSource=presets,SelectedItem=presets.FirstOrDefault(p=>p.Id.Equals(selected,StringComparison.OrdinalIgnoreCase))};
   choices.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal'><Border Width='18' Height='18' CornerRadius='4' Background='{Binding Color}' Margin='0,0,8,0'/><TextBlock Text='{Binding Name}'/></StackPanel></DataTemplate>");RocketAppearancePanel.Children.Add(choices);
   var input=new TextBox{Text=selected,ToolTip="Custom colour as #RRGGBB, for example #195CB8.",MaxLength=7,Margin=new Thickness(0,5,0,0)};
   choices.SelectionChanged+=(_,_)=>{if(choices.SelectedItem is CosmeticStyleOption option)input.Text=option.Id;};
   input.TextChanged+=(_,_)=>{update(input.Text);changed();if(RocketAppearance.ValidColor(input.Text))input.BorderBrush=(Brush)FindResource("Line");else input.BorderBrush=Brushes.IndianRed;};RocketAppearancePanel.Children.Add(input);
  }
  if(asset.Category=="Cars"){
   color("CAR COLOUR",appearance.Primary,x=>appearance.Primary=x);color("DECAL COLOUR",appearance.Secondary,x=>appearance.Secondary=x);
   combo("CAR FINISH",RocketAppearance.Finishes,appearance.PrimaryFinish,x=>appearance.PrimaryFinish=x);combo("DECAL FINISH",RocketAppearance.Finishes,appearance.SecondaryFinish,x=>appearance.SecondaryFinish=x);
  }
  if(asset.Category is "Cars" or "Wheels"){
   var paint=combo(asset.Category=="Cars"?"PAINTED TRIM":"PAINT VARIANT",RocketAppearance.Paints,appearance.BodyPaint,x=>appearance.BodyPaint=x);paint.IsEnabled=false;paint.ToolTip="Available when the source material exposes a paint-colour parameter.";_ = EnableRocketPaint(asset,paint,appearance,false);
  }
  if(asset.Category=="Cars"){
   label("WHEELS");var wheelChoices=new[]{new Cosmetic("","No wheels","Wheels","")}.Concat(_rocketSession.Wheels).ToArray();var wheels=new ComboBox{ItemsSource=wheelChoices,DisplayMemberPath="Name",IsTextSearchEnabled=true,SelectedItem=wheelChoices.FirstOrDefault(w=>w.Id==appearance.WheelId)??wheelChoices[0],MaxDropDownHeight=320};RocketAppearancePanel.Children.Add(wheels);appearance.WheelId=((Cosmetic)wheels.SelectedItem).Id;if(appearance.WheelId.Length==0)appearance.WheelPaint="Default";
   var wheelPaint=combo("WHEEL PAINT",RocketAppearance.Paints,appearance.WheelPaint,x=>appearance.WheelPaint=x);wheelPaint.IsEnabled=false;
   wheels.SelectionChanged+=(_,_)=>{if(wheels.SelectedItem is Cosmetic wheel){appearance.WheelId=wheel.Id;wheelPaint.IsEnabled=false;if(wheel.Id.Length>0)_=EnableRocketPaint(wheel,wheelPaint,appearance,true);else{appearance.WheelPaint="Default";wheelPaint.SelectedItem="Default";}changed();}};
   if(wheels.SelectedItem is Cosmetic selectedWheel && selectedWheel.Id.Length>0)_=EnableRocketPaint(selectedWheel,wheelPaint,appearance,true);
  }
  var button=new Button{Content="Generate preview",Margin=new Thickness(0,16,0,6),ToolTip="Render the selected model with these colours, decal, wheels and finishes. The same settings are used for export."};button.Click+=async(_,_)=>await PreviewRocketAsset(asset);RocketAppearancePanel.Children.Add(button);
  _rocketPreviewStatus=new TextBlock{Text="Preview uses your selected export settings. Custom Unreal shaders are simplified.",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=(Brush)FindResource("Muted"),Margin=new Thickness(0,4,0,8)};RocketAppearancePanel.Children.Add(_rocketPreviewStatus);
  _rocketPreviewImage=new Image{Stretch=Stretch.Uniform,MaxHeight=260};RocketAppearancePanel.Children.Add(_rocketPreviewImage);
 }
 private async Task EnableRocketPaint(Cosmetic asset,ComboBox control,RocketAppearance appearance,bool wheel)
 {
  try{bool supported=await _rocketSession.SupportsPaint(asset,_lifetime.Token);if(!IsRocketLeague||!RocketAppearancePanel.IsAncestorOf(control)||(wheel && appearance.WheelId!=asset.Id))return;control.IsEnabled=supported;if(!supported){if(wheel)appearance.WheelPaint="Default";else appearance.BodyPaint="Default";control.SelectedItem="Default";control.ToolTip="This source material does not expose a supported paint-colour parameter.";}}
  catch(OperationCanceledException){}
  catch(Exception){control.ToolTip="Could not read paint support. Rescan the game and select this asset again.";}
 }
 private async Task PreviewRocketAsset(Cosmetic asset)
 {
  if(_operation!=null||!IsRocketLeague)return;
  var appearance=_rocketAppearances.GetValueOrDefault(asset.Id)??new();var image=_rocketPreviewImage;var status=_rocketPreviewStatus;
  await Run(async(_,progress,ct)=>{
   if(status!=null)status.Text="Rendering selected appearance…";
   string cacheRoot=Path.Combine(AppSettings.DataFolder,"rl-previews");Directory.CreateDirectory(cacheRoot);
   string key=System.Text.Json.JsonSerializer.Serialize(new{asset.Id,asset.ObjectPath,Appearance=appearance,Style=_selectedStyles.GetValueOrDefault(asset.Id,"Default"),Version="0.7.2",Stamp=File.Exists(asset.ObjectPath.Split('|')[0])?File.GetLastWriteTimeUtc(asset.ObjectPath.Split('|')[0]).Ticks:0});
   string cached=Path.Combine(cacheRoot,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))+".png");
   if(File.Exists(cached)){var saved=new BitmapImage();saved.BeginInit();saved.CacheOption=BitmapCacheOption.OnLoad;saved.UriSource=new Uri(cached);saved.EndInit();saved.Freeze();if(image!=null)image.Source=saved;if(status!=null)status.Text="Preview of the selected appearance.";return;}
   var settings=new AppSettings{ExportFolder=cacheRoot,ExportFormat="BLEND"};
   var result=await _rocketSession.Export(asset,_selectedStyles.GetValueOrDefault(asset.Id,"Default"),settings,progress,ct,appearance,true);
   string path=Path.Combine(result.Folder,"preview.png");
   if(File.Exists(path)){File.Copy(path,cached,true);path=cached;}
   if(File.Exists(path)&&image!=null){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();image.Source=bitmap;if(status!=null)status.Text="Preview of the selected appearance. "+(result.Warnings.Length>0?"Some custom shader inputs are simplified.":"");}
   else if(status!=null)status.Text="No model or source thumbnail is available for this package.";
   string resolved=Path.GetFullPath(result.Folder),owned=Path.GetFullPath(cacheRoot)+Path.DirectorySeparatorChar;
   if(resolved.StartsWith(owned,StringComparison.OrdinalIgnoreCase))Directory.Delete(resolved,true);
  });
 }
}
