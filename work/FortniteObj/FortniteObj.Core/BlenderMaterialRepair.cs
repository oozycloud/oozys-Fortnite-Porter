using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public static class BlenderMaterialRepair
{
	public static string CreateCopy(string sourceBlend, string exportRoot, IProgress<string> progress, CancellationToken ct)
	{
		sourceBlend = Path.GetFullPath(sourceBlend);
		if (!File.Exists(sourceBlend) || !sourceBlend.EndsWith(".blend", StringComparison.OrdinalIgnoreCase))
		{
			throw new IOException("Choose an existing .blend file.");
		}
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
		string text = (Directory.Exists(path) ? Directory.EnumerateFiles(path, "blender.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault() : null);
		if (text == null)
		{
			throw new IOException("Blender was not found under Program Files / Blender Foundation.");
		}
		ct.ThrowIfCancellationRequested();
		string text2 = Installation.SafeName(Path.GetFileNameWithoutExtension(sourceBlend)) + "_Matte";
		string text3 = Path.Combine(Path.GetFullPath(exportRoot), text2 + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text3);
		string text4 = Path.Combine(text3, text2 + ".blend");
		string text5 = Path.Combine(text3, "blender_matte_copy.py");
		using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteObj.blender_matte_copy.py") ?? throw new IOException("Material repair script is missing from this app build."))
		{
			using FileStream destination = File.Create(text5);
			stream.CopyTo(destination);
		}
		progress.Report("Softening materials in " + Path.GetFileName(sourceBlend) + "; saving a separate copy…");
		ProcessStartInfo processStartInfo = new ProcessStartInfo(text)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = text3
		};
		string[] array = new string[10] { "--background", "--factory-startup", "--disable-autoexec", "--python-exit-code", "1", "--python", text5, "--", sourceBlend, text4 };
		foreach (string item in array)
		{
			processStartInfo.ArgumentList.Add(item);
		}
		using Process process = Process.Start(processStartInfo) ?? throw new IOException("Could not start Blender.");
		Task<string> task = process.StandardOutput.ReadToEndAsync();
		Task<string> task2 = process.StandardError.ReadToEndAsync();
		using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5L));
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
		File.WriteAllText(Path.Combine(text3, "blender.log"), task.GetAwaiter().GetResult() + task2.GetAwaiter().GetResult());
		if (process.ExitCode != 0 || !File.Exists(text4))
		{
			throw new IOException("Material repair failed. Check " + Path.Combine(text3, "blender.log"));
		}
		progress.Report("Saved matte copy: " + text4);
		return text4;
	}
}
