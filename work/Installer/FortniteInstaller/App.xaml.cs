using System;
using System.IO;
using System.Windows;

namespace FortniteInstaller;

public partial class App : Application
{
	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		if (e.Args.Length == 2 && e.Args[0] == "--ui-check")
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			try
			{
				await UiDiagnostics.Run(Path.GetFullPath(e.Args[1]));
				Shutdown(0);
				return;
			}
			catch (Exception ex)
			{
				Directory.CreateDirectory(e.Args[1]);
				File.WriteAllText(Path.Combine(e.Args[1], "error.txt"), ex.ToString());
				Shutdown(1);
				return;
			}
		}
		if (e.Args.Length == 2 && e.Args[0] == "--extract-to")
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			try
			{
				Payload.Extract(e.Args[1]);
				Shutdown(0);
				return;
			}
			catch (Exception ex2)
			{
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "Porter-install-error.txt"), ex2.ToString());
				Shutdown(1);
				return;
			}
		}
		new MainWindow().Show();
	}
}
