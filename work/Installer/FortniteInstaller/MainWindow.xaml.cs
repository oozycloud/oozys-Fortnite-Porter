using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using PorterRelease;

namespace FortniteInstaller;

public partial class MainWindow : Window, IComponentConnector
{
	private bool _installing;

	private int _locationRevision;

	private static string LocationRecord => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteObj", "install-location.txt");

	public MainWindow()
	{
		InitializeComponent();
		string[] source = new string[3]
		{
			AppContext.BaseDirectory,
			File.Exists(LocationRecord) ? File.ReadAllText(LocationRecord) : "",
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fortnite Porter Studio")
		};
		Folder.Text = source.FirstOrDefault((string x) =>
		{
			try
			{
				return !string.IsNullOrWhiteSpace(x) && Payload.Detect(x) != null;
			}
			catch
			{
				return false;
			}
		}) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "oozys-Fortnite-Porter");
	}

	private async void FolderChanged(object s, TextChangedEventArgs e)
	{
		if (InstallButton == null || _installing)
		{
			return;
		}
		int revision = ++_locationRevision;
		InstallButton.IsEnabled = false;
		Status.Text = "Checking location…";
		string folder = Folder.Text;
		await Task.Delay(250);
		if (revision != _locationRevision)
		{
			return;
		}
		try
		{
			InstalledProduct installedProduct = await Task.Run(() => Payload.Detect(folder));
			if (revision == _locationRevision)
			{
				InstallButton.Content = (((object)installedProduct == null) ? "Install" : "Update");
				Heading.Text = (((object)installedProduct == null) ? "Choose where to install" : "Update your installation");
				bool flag = (object)installedProduct != null && ProductInfo.IsNewerInstallation(installedProduct.Version);
				TextBlock status = Status;
				string text;
				if (flag)
				{
					text = "Version " + installedProduct.Version + " is already installed. This installer cannot downgrade it.";
				}
				else
				{
					text = (((object)installedProduct == null) ? "Ready to install version beta 0.6.0." : ("Found version " + installedProduct.Version + ". Update will install beta 0.6.0 and keep your settings and exports."));
				}
				status.Text = text;
				InstallButton.IsEnabled = !flag;
			}
		}
		catch (Exception ex)
		{
			if (revision == _locationRevision)
			{
				Status.Text = ex.Message;
			}
		}
	}

	private void Browse(object s, RoutedEventArgs e)
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			SelectedPath = Folder.Text,
			Description = "Choose an application folder"
		};
		try
		{
			if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Folder.Text = folderBrowserDialog.SelectedPath;
			}
		}
		finally
		{
			((IDisposable)(object)folderBrowserDialog)?.Dispose();
		}
	}

	private void Cancel(object s, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (_installing)
		{
			e.Cancel = true;
		}
		base.OnClosing(e);
	}

	private async void Install(object s, RoutedEventArgs e)
	{
		_installing = true;
		_locationRevision++;
		System.Windows.Controls.Button installButton = InstallButton;
		System.Windows.Controls.Button browseButton = BrowseButton;
		System.Windows.Controls.Button cancelButton = CancelButton;
		bool flag = (Folder.IsEnabled = false);
		bool flag3 = (cancelButton.IsEnabled = flag);
		bool isEnabled = (browseButton.IsEnabled = flag3);
		installButton.IsEnabled = isEnabled;
		string folder = Folder.Text;
		try
		{
			Progress<string> progress = new Progress<string>((string text) =>
			{
				Status.Text = text;
			});
			await Task.Run(() =>
			{
				Payload.Extract(folder, progress);
			});
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(LocationRecord));
				File.WriteAllText(LocationRecord, Payload.Root(folder));
			}
			catch (IOException)
			{
			}
			Status.Text = "Version beta 0.6.0 is ready.";
			InstallButton.Content = "Update";
			System.Windows.MessageBox.Show(this, "Open oozys-Fortnite-Porter (beta 0.6.0).exe from:\n\n" + Payload.Root(folder), "Installation complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex2)
		{
			Status.Text = ex2.Message;
			System.Windows.MessageBox.Show(this, ex2.Message, "Installation could not finish", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		finally
		{
			_installing = false;
			System.Windows.Controls.Button installButton2 = InstallButton;
			System.Windows.Controls.Button browseButton2 = BrowseButton;
			System.Windows.Controls.Button cancelButton2 = CancelButton;
			flag = (Folder.IsEnabled = true);
			flag3 = (cancelButton2.IsEnabled = flag);
			isEnabled = (browseButton2.IsEnabled = flag3);
			installButton2.IsEnabled = isEnabled;
		}
	}
}
