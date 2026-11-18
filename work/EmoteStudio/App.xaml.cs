using System.Windows;
using System.IO;
namespace EmoteStudio;
public partial class App:Application {
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  try {var window=new MainWindow(e.Args);MainWindow=window;window.Show();}
  catch(Exception error) {
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"oozycloud","EmoteStudio");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"startup-error.txt"),error.ToString());
   if(e.Args.Length>1&&e.Args[0]=="--smoke-test"){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());}
   else MessageBox.Show("Emote Studio could not open.\n"+error.Message,"oozy's Emote Studio",MessageBoxButton.OK,MessageBoxImage.Error);
   Shutdown(1);
  }
 }
}
