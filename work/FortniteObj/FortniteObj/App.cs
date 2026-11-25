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
        if(e.Args.FirstOrDefault()=="--customization-preview-check"){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;string folder=Path.GetFullPath(e.Args.ElementAtOrDefault(1)??"customization-preview-check");Directory.CreateDirectory(folder);
            string settingsFile=Path.Combine(AppSettings.DataFolder,"settings.json");byte[]? savedSettings=File.Exists(settingsFile)?File.ReadAllBytes(settingsFile):null;
            var window=new MainWindow(checkUpdates:false){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0};window.Show();
            try{await window.CheckCustomizationPreview(folder,e.Args.ElementAtOrDefault(2)??"Tour-Ready Sabrina Carpenter");window.Close();Shutdown(0);}
            catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());await window.StopSkinPreviewAsync();window.Close();Shutdown(1);}
            finally{if(savedSettings!=null)File.WriteAllBytes(settingsFile,savedSettings);else if(File.Exists(settingsFile))File.Delete(settingsFile);}return;
        }
        if(e.Args.FirstOrDefault()=="--cards-check"){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;string folder=Path.GetFullPath(e.Args.ElementAtOrDefault(1)??"cards-check");Directory.CreateDirectory(folder);
            string settingsFile=Path.Combine(AppSettings.DataFolder,"settings.json");byte[]? savedSettings=File.Exists(settingsFile)?File.ReadAllBytes(settingsFile):null;
            var window=new MainWindow(checkUpdates:false){ShowInTaskbar=false};window.Show();
            try{await window.CheckCardGallery(folder);window.Close();Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());window.Close();Shutdown(1);}
            finally{if(savedSettings!=null)File.WriteAllBytes(settingsFile,savedSettings);else if(File.Exists(settingsFile))File.Delete(settingsFile);}return;
        }
        if(e.Args.FirstOrDefault()=="--studio-check"){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            string folder=Path.GetFullPath(e.Args.ElementAtOrDefault(1)??"studio-check");Directory.CreateDirectory(folder);
            var window=new MainWindow(checkUpdates:false){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0};window.Show();
            try{await window.RunMergedDiagnostics(folder,e.Args.ElementAtOrDefault(2),e.Args.Skip(3).ToArray());window.Close();if(Windows.Count!=0)throw new IOException("A hidden editor window remained after closing the shell.");Shutdown(0);}
            catch(Exception ex){File.WriteAllText(Path.Combine(folder,"merged-error.txt"),ex.ToString());window.Close();Shutdown(1);}return;
        }
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
        if(e.Args.FirstOrDefault()=="--template-check"){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;string folder=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(folder);
            try{
                File.Copy(e.Args[2],Path.Combine(folder,Path.GetFileName(e.Args[2])),true);
                var result=await VehicleTextureTemplates.Create(new ExportResult(folder,0,0,[]),new DirectProgress(_=>{}),CancellationToken.None);
                File.WriteAllText(Path.Combine(folder,"template-result.json"),JsonSerializer.Serialize(result));Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());Shutdown(1);}return;
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
                    int auditArg=Array.IndexOf(e.Args,"--audit-items");
                    if(auditArg>=0 && auditArg+1<e.Args.Length){
                        foreach(string auditName in e.Args[auditArg+1].Split('|')){
                            Cosmetic candidate=list.First(x=>x.Category=="Skins" && x.Name.Equals(auditName,StringComparison.OrdinalIgnoreCase));
                            var selections=new List<Dictionary<string,string>>{new()};
                            var channels=session.GetStyleChannels(candidate);
                            if(e.Args.Contains("--audit-styles"))foreach(var channel in channels)selections=selections.SelectMany(selection=>channel.Options.Select(option=>{var next=new Dictionary<string,string>(selection);next[channel.Id]=option.Id;return next;})).ToList();
                            foreach(var selection in selections){
                                string selected=selection.Count>0?StyleSelection.Encode(selection):"Default";
                                var result=await Task.Run(()=>session.Export(candidate,folder2,progress,CancellationToken.None,selected,false,false,0,false));
                                File.WriteAllText(Path.Combine(result.Folder,"style-check.json"),JsonSerializer.Serialize(selection.ToDictionary(x=>channels.First(c=>c.Id==x.Key).Name,x=>channels.First(c=>c.Id==x.Key).Options.First(o=>o.Id==x.Value).Name)));
                                progress.Report(JsonSerializer.Serialize(result));
                            }
                        }
                    }
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
