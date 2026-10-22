using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public static class BlenderExport
{
	public static void Create(string folder, List<string> warnings, IProgress<string> progress, CancellationToken ct, bool required = false)
	{
		string text = Path.Combine(folder, "blender_materials.py");
		string[] array = new string[6] { "blender_materials.py", "blender_rig.py", "blender_animation.py", "blender_fx.py", "blender_physics.py", "blender_surface_fx.py" };
		foreach (string text2 in array)
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteObj." + text2);
			using FileStream destination = File.Create(Path.Combine(folder, text2));
			stream.CopyTo(destination);
		}
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
		string text3 = (Directory.Exists(path) ? Directory.EnumerateFiles(path, "blender.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault() : null);
		if (text3 == null)
		{
			if (required)
			{
				throw new IOException("Blender is required to create the skin + emote scene. Install Blender, then export again.");
			}
			warnings.Add("Blender was not found. OBJ is ready; run blender_materials.py in Blender to create a packed .blend with corrected material colour spaces.");
			return;
		}
		progress.Report("Creating the Blender rig, bone weights, materials and packed textures…");
		StreamWriter log;
		object logLock;
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(text3)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = folder
			};
			array = new string[8]
			{
				"--background",
				"--factory-startup",
				"--python-exit-code",
				"1",
				"--python",
				text,
				"--",
				Path.Combine(folder, "materials.json")
			};
			foreach (string item in array)
			{
				processStartInfo.ArgumentList.Add(item);
			}
			using Process process = Process.Start(processStartInfo) ?? throw new IOException("Could not start Blender.");
			log = new StreamWriter(Path.Combine(folder, "blender.log"))
			{
				AutoFlush = true
			};
			try
			{
				logLock = new object();
				Task task = Pump(process.StandardOutput);
				Task task2 = Pump(process.StandardError);
				using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
				cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5L));
				try
				{
					process.WaitForExitAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					if (!process.HasExited)
					{
						process.Kill(entireProcessTree: true);
					}
					throw new IOException("Blender exceeded the five-minute conversion limit. Progress was saved in blender.log.");
				}
				finally
				{
					if (!process.HasExited)
					{
						process.Kill(entireProcessTree: true);
					}
					InlineArray2<Task> buffer = default;
					buffer[0] = task;
					buffer[1] = task2;
					Task.WhenAll(buffer).GetAwaiter().GetResult();
				}
				if (process.ExitCode != 0)
				{
					throw new IOException("Blender conversion failed; see blender.log.");
				}
			}
			finally
			{
				if (log != null)
				{
					((IDisposable)log).Dispose();
				}
			}
		}
		catch (Exception ex2) when (!ct.IsCancellationRequested && !required)
		{
			warnings.Add("OBJ exported, but the Blender file could not be created: " + ex2.Message);
		}
		async Task Pump(StreamReader reader)
		{
			while (true)
			{
				string text4 = await reader.ReadLineAsync();
				if (text4 == null)
				{
					break;
				}
				lock (logLock)
				{
					log.WriteLine(text4);
				}
				if (text4.StartsWith("PORTER: ", StringComparison.Ordinal))
				{
					progress.Report(text4.Substring(8));
				}
			}
		}
	}
}
