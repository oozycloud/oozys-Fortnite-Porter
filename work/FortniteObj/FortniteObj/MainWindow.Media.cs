using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using FortniteObj.Core;

namespace FortniteObj;
public partial class MainWindow
{
    private void ChooseRadaDecoder_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Title="Choose RAD audio decoder v1.0",Filter="RAD audio decoder|radadec.exe",CheckFileExists=true};
        if(dialog.ShowDialog(this)!=true)return;
        try {
            byte[] bytes=File.ReadAllBytes(dialog.FileName);
            if(Convert.ToHexString(SHA256.HashData(bytes))!="4D0022C501D61EA97495EDD58645C82AA78461B08E6BE2AE79FC37A7B7B3A7F2")throw new IOException("Choose the supported radadec.exe v1.0 from the decoder's official release. See the audio section in README.");
            string folder=Path.Combine(AppSettings.DataFolder,"audio-tools");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"radadec.exe"),bytes);Report("RAD audio decoder ready.");
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){MessageBox.Show(this,ex.Message,"Audio decoder",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
