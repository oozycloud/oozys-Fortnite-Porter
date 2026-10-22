using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FortniteObj.Core;

namespace FortniteObj;

public class App : Application
{
	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		if (e.Args.FirstOrDefault() == "--ui-check")
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			string folder = Path.GetFullPath(e.Args.ElementAtOrDefault(1) ?? "ui-check");
			try
			{
				await UiDiagnostics.Run(folder);
				Shutdown(0);
				return;
			}
			catch (Exception ex)
			{
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder, "error.txt"), ex.ToString());
				Shutdown(1);
				return;
			}
		}
		if (e.Args.FirstOrDefault() == "--diagnose")
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			string folder2 = Path.GetFullPath(e.Args.ElementAtOrDefault(1) ?? "diagnostics");
			Directory.CreateDirectory(folder2);
			DirectProgress progress = new DirectProgress((string x) =>
			{
				File.AppendAllText(Path.Combine(folder2, "scan.log"), x + "\n");
			});
			try
			{
				GameSession session = new GameSession();
				try
				{
					AppSettings appSettings = AppSettings.Load();
					appSettings.GameFolder = Installation.Detect()?.Path ?? appSettings.GameFolder;
					if (e.Args.Length > 2 && e.Args[2].StartsWith("GAME_"))
					{
						appSettings.EngineVersion = e.Args[2];
					}
					List<Cosmetic> list = await session.Scan(appSettings, progress, CancellationToken.None);
					File.WriteAllText(Path.Combine(folder2, "catalog.json"), JsonSerializer.Serialize(list, new JsonSerializerOptions
					{
						WriteIndented = true
					}));
					File.WriteAllLines(Path.Combine(folder2, "warnings.txt"), session.Warnings);
					int inspectArg = Array.IndexOf(e.Args, "--inspect-items");
					if (inspectArg >= 0 && inspectArg + 1 < e.Args.Length)
					{
						session.InspectDefinitions(folder2, list.Where((Cosmetic x) => e.Args[inspectArg + 1].Split('|').Contains(x.Name)));
					}
					session.Inspect(folder2, list.Where((Cosmetic x) => x.Category == "Skins").Take(1).Concat(list.Where((Cosmetic x) => x.Category == "Pickaxes").Take(1)));
					int itemArg = Array.IndexOf(e.Args, "--item");
					int sceneSkinArg = Array.IndexOf(e.Args, "--scene-skin");
					int sceneEmoteArg = Array.IndexOf(e.Args, "--scene-emote");
					if (sceneSkinArg >= 0 && sceneEmoteArg >= 0 && sceneSkinArg + 1 < e.Args.Length && sceneEmoteArg + 1 < e.Args.Length)
					{
						Cosmetic sceneSkin = list.First((Cosmetic x) => x.Category == "Skins" && (x.Name.Equals(e.Args[sceneSkinArg + 1], StringComparison.OrdinalIgnoreCase) || x.Id.Equals(e.Args[sceneSkinArg + 1], StringComparison.OrdinalIgnoreCase)));
						Cosmetic sceneEmote = list.First((Cosmetic x) => x.Category == "Animations" && (x.Name.Equals(e.Args[sceneEmoteArg + 1], StringComparison.OrdinalIgnoreCase) || x.Id.Equals(e.Args[sceneEmoteArg + 1], StringComparison.OrdinalIgnoreCase)));
						int num = Array.IndexOf(e.Args, "--scene-name");
						string sceneName = ((num >= 0 && num + 1 < e.Args.Length) ? e.Args[num + 1] : (sceneSkin.Name + "_" + sceneEmote.Name));
						ExportResult value = await Task.Run(() => session.ExportScene(sceneSkin, sceneEmote, folder2, sceneName, progress, CancellationToken.None, "Default", e.Args.Contains("--only-blend"), !e.Args.Contains("--source-materials")));
						progress.Report(JsonSerializer.Serialize(value));
					}
					if (itemArg >= 0 && itemArg + 1 < e.Args.Length)
					{
						Cosmetic item = list.First((Cosmetic x) => x.Name.Equals(e.Args[itemArg + 1], StringComparison.OrdinalIgnoreCase) || x.Id.Equals(e.Args[itemArg + 1], StringComparison.OrdinalIgnoreCase));
						if (e.Args.Contains("--styles"))
						{
							progress.Report("Styles: " + string.Join(", ", session.GetStyles(item)));
						}
						if (item.Category != "Terrain")
						{
							session.InspectQuality(item, folder2);
						}
						int num2 = Array.IndexOf(e.Args, "--style");
						string style = ((num2 >= 0 && num2 + 1 < e.Args.Length) ? e.Args[num2 + 1] : "Default");
						if (e.Args.Contains("--styles-only"))
						{
							File.WriteAllText(Path.Combine(folder2, "styles.txt"), string.Join(Environment.NewLine, session.GetStyles(item)));
							Shutdown(0);
							return;
						}
						ExportResult value2 = await Task.Run(() => session.Export(item, folder2, progress, CancellationToken.None, style, e.Args.Contains("--only-blend"), !e.Args.Contains("--source-materials")));
						progress.Report(JsonSerializer.Serialize(value2));
					}
					if (e.Args.Contains("--export-smoke"))
					{
						string[] array = new string[4] { "Skins", "Pickaxes", "Back blings", "Gliders" };
						foreach (string category in array)
						{
							foreach (Cosmetic candidate in (from x in list
								where x.Category == category
								orderby (!x.Id.StartsWith("CID_")) ? 1 : 0
								select x).Take(8))
							{
								try
								{
									ExportResult exportResult = await Task.Run(() => session.Export(candidate, folder2, progress, CancellationToken.None));
									progress.Report(JsonSerializer.Serialize(exportResult));
									if (exportResult.Textures > 0)
									{
										break;
									}
								}
								catch (Exception ex2)
								{
									progress.Report(candidate.Id + ": " + ex2);
								}
							}
						}
					}
					File.WriteAllText(Path.Combine(folder2, "success.txt"), $"{list.Count} cosmetics from {session.FileCount} files");
					Shutdown(0);
				}
				finally
				{
					if (session != null)
					{
						((IDisposable)session).Dispose();
					}
				}
				return;
			}
			catch (Exception ex3)
			{
				File.WriteAllText(Path.Combine(folder2, "error.txt"), ex3.ToString());
				Shutdown(1);
				return;
			}
		}
		new MainWindow().Show();
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public static void Main()
	{
		new App().Run();
	}
}
