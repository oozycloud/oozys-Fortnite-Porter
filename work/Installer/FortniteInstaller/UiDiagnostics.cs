using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FortniteInstaller;

internal static class UiDiagnostics
{
	internal static async Task Run(string folder)
	{
		Directory.CreateDirectory(folder);
		MainWindow window = new MainWindow
		{
			ShowInTaskbar = false
		};
		window.Show();
		try
		{
			TextBox path = (TextBox)window.FindName("Folder");
			Button install = (Button)window.FindName("InstallButton");
			path.Text = Path.Combine(folder, "new installation");
			await Task.Delay(650);
			if ((string)install.Content != "Install" || !install.IsEnabled)
			{
				throw new Exception("Fresh location did not offer Install.");
			}
			Save(window, Path.Combine(folder, "install.png"));
			string legacy = Path.Combine(folder, "old installation");
			Directory.CreateDirectory(legacy);
			File.WriteAllText(Path.Combine(legacy, "Fortnite Porter Studio v2.1.0.exe"), "fixture");
			File.WriteAllText(Path.Combine(legacy, "Fortnite Porter Studio v2.1.0.runtimeconfig.json"), "{}");
			path.Text = legacy;
			await Task.Delay(650);
			if ((string)install.Content != "Update" || !install.IsEnabled)
			{
				throw new Exception("Old installation did not offer Update.");
			}
			Save(window, Path.Combine(folder, "update.png"));
			path.Text = "";
			await Task.Delay(650);
			if (install.IsEnabled)
			{
				throw new Exception("Empty path enabled Install.");
			}
			path.Text = Path.GetPathRoot(folder);
			await Task.Delay(650);
			if (install.IsEnabled)
			{
				throw new Exception("Drive root enabled Install.");
			}
			path.Text = legacy;
			path.Text = Path.Combine(folder, "new location");
			await Task.Delay(650);
			if ((string)install.Content != "Install" || !install.IsEnabled)
			{
				throw new Exception("Stale location detection won the race.");
			}
			string text = Path.Combine(folder, "newer installation");
			Directory.CreateDirectory(Path.Combine(text, "app"));
			File.WriteAllText(Path.Combine(text, "app", "installation.json"), JsonSerializer.Serialize(new InstalledProduct("oozycloud.fortnite-porter", "9.0.0", "oozys-Fortnite-Porter (9.0.0).exe", new string[1] { "app/Porter.exe" })));
			path.Text = text;
			await Task.Delay(650);
			if (install.IsEnabled)
			{
				throw new Exception("Installer offered a downgrade.");
			}
			File.WriteAllText(Path.Combine(folder, "result.json"), JsonSerializer.Serialize(new
			{
				FreshInstall = true,
				LegacyUpdate = true,
				EmptyPathRejected = true,
				DriveRootRejected = true,
				LatestLocationWins = true,
				DowngradeRejected = true
			}));
		}
		finally
		{
			window.Close();
		}
	}

	private static void Save(Window window, string path)
	{
		window.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(window);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}
}
