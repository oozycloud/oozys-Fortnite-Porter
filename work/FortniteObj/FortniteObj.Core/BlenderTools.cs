using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public static class BlenderTools
{
	public static string Install(IProgress<string> progress, CancellationToken ct)
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
		string text = (Directory.Exists(path) ? Directory.EnumerateFiles(path, "blender.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault() : null);
		if (text == null)
		{
			throw new IOException("Blender was not found under Program Files.");
		}
		string text2 = Path.Combine(AppSettings.DataFolder, "BlenderTools");
		Directory.CreateDirectory(text2);
		string text3 = Path.Combine(text2, "PorterEmoteTools.zip");
		using (FileStream stream = File.Create(text3))
		{
			using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
			(string, string)[] array = new (string, string)[2]
			{
				("blender_emote_tools.py", "__init__.py"),
				("blender_animation.py", "blender_animation.py")
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (text4, text5) = array[i];
				using Stream stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteObj." + text4);
				using Stream destination = zipArchive.CreateEntry("porter_emote_tools/" + text5).Open();
				stream2.CopyTo(destination);
			}
		}
		string text6 = Path.Combine(text2, "install.py");
		File.WriteAllText(text6, "import bpy,sys\npath=sys.argv[sys.argv.index('--')+1]\nbpy.ops.preferences.addon_install(filepath=path,overwrite=True)\nbpy.ops.preferences.addon_enable(module='porter_emote_tools')\nbpy.ops.wm.save_userpref()\nprint('Porter Emote Tools installed')\n");
		progress.Report("Installing the emote importer in Blender…");
		ProcessStartInfo processStartInfo = new ProcessStartInfo(text)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		string[] array2 = new string[7] { "--background", "--python-exit-code", "1", "--python", text6, "--", text3 };
		foreach (string item in array2)
		{
			processStartInfo.ArgumentList.Add(item);
		}
		using Process process = Process.Start(processStartInfo) ?? throw new IOException("Could not start Blender.");
		Task<string> task = process.StandardOutput.ReadToEndAsync();
		Task<string> task2 = process.StandardError.ReadToEndAsync();
		using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2L));
		try
		{
			process.WaitForExitAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
		}
		catch
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
			throw;
		}
		File.WriteAllText(Path.Combine(text2, "install.log"), task.GetAwaiter().GetResult() + task2.GetAwaiter().GetResult());
		if (process.ExitCode != 0)
		{
			throw new IOException("Blender could not install the emote tools. See " + Path.Combine(text2, "install.log"));
		}
		return text;
	}
}
