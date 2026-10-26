using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FortniteObj.Core;

namespace FortniteObj;

internal static class UiDiagnostics
{
	private sealed class UpdateFixture : HttpMessageHandler
	{
		internal int Calls;

		internal HttpStatusCode Code = HttpStatusCode.OK;

		internal string Body = "[{\"tag_name\":\"v9.0.0\",\"html_url\":\"https://github.com/oozycloud/oozys-Fortnite-Porter/releases/tag/v9.0.0\"}]";

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			Calls++;
			return Task.FromResult(new HttpResponseMessage(Code)
			{
				Content = new StringContent(Body)
			});
		}
	}

	internal static async Task Run(string folder)
	{
		Directory.CreateDirectory(folder);
		string settingsPath = Path.Combine(AppSettings.DataFolder, "settings.json");
		byte[] settings = (File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null);
		string logPath = Path.Combine(AppSettings.DataFolder, "last-session.log");
		byte[] previousLog = (File.Exists(logPath) ? File.ReadAllBytes(logPath) : null);
		UpdateFixture handler = new UpdateFixture();
		MainWindow window = new MainWindow(checkUpdates: false, new HttpClient(handler))
		{
			ShowInTaskbar = false,
			Width = 1500.0,
			Height = 960.0
		};
		try
		{
            // Archive readers block synchronously on the UI thread; cached async IO must not capture it.
            string indexHash = "ui-context-" + Guid.NewGuid().ToString("N");
            string indexFolder = Path.Combine(AppSettings.DataFolder, "texture-chunks");
            Directory.CreateDirectory(indexFolder);
            string indexFixture = Path.Combine(indexFolder, indexHash + ".utoc");
            byte[] indexBytes = Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i % 251)).ToArray();
            File.WriteAllBytes(indexFixture, indexBytes);
            try
            {
                if (SynchronizationContext.Current == null) throw new Exception("Archive index test requires the UI synchronization context.");
                byte[] cachedIndex = ArchiveIndexDownload.Get("unused", indexHash, "UI context fixture", null,
                    bytes => { if (!bytes.SequenceEqual(indexBytes)) throw new Exception("Cached index changed."); }, CancellationToken.None).GetAwaiter().GetResult();
                if (!cachedIndex.SequenceEqual(indexBytes)) throw new Exception("Archive reader returned different bytes.");
            }
            finally { File.Delete(indexFixture); }
			window.Show();
            ((ListBox)window.FindName("GameSelector")).SelectedIndex = 0;
			await Task.Delay(500);
			((TabControl)window.FindName("WorkspaceTabs")).SelectedIndex = 1;
			if (handler.Calls != 0)
			{
				throw new InvalidOperationException("Disabled startup update check made a request.");
			}
			((CheckBox)window.FindName("AutoUpdateCheck")).IsChecked = false;
			if (AppSettings.Load().CheckForUpdatesAutomatically)
			{
				throw new InvalidOperationException("Automatic update preference was not saved.");
			}
			((Button)window.FindName("CheckUpdateButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
			await Task.Delay(100);
			if (handler.Calls != 1 || ((Button)window.FindName("UpdateNotice")).Visibility != Visibility.Visible)
			{
				throw new InvalidOperationException("Manual update notice failed.");
			}
			Save(window, Path.Combine(folder, "update-available.png"));
			handler.Body = "{}";
			handler.Code = HttpStatusCode.NotFound;
			((Button)window.FindName("CheckUpdateButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
			await Task.Delay(100);
			if (((Button)window.FindName("UpdateNotice")).Visibility != Visibility.Collapsed)
			{
				throw new InvalidOperationException("Stale update notice was not cleared.");
			}
			((CheckBox)window.FindName("ReduceMotion")).IsChecked = false;
			ComboBox theme = (ComboBox)window.FindName("AnimatedTheme");
			theme.BringIntoView();
			string[] array = new string[3] { "Embers", "Orbit", "Rain" };
			for (int i = 0; i < array.Length; i++)
			{
				string name = (string)(theme.SelectedItem = array[i]);
				await Task.Delay(650);
				Save(window, Path.Combine(folder, name + ".png"));
				Save((FrameworkElement)window.FindName("Backdrop"), Path.Combine(folder, name + "-motion-1.png"));
				await Task.Delay(300);
				Save((FrameworkElement)window.FindName("Backdrop"), Path.Combine(folder, name + "-motion-2.png"));
				if (File.ReadAllBytes(Path.Combine(folder, name + "-motion-1.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, name + "-motion-2.png"))))
				{
					throw new InvalidOperationException(name + " did not animate.");
				}
			}
			theme.IsDropDownOpen = true;
			await Task.Delay(400);
			Save((FrameworkElement)((Popup)theme.Template.FindName("PART_Popup", theme)).Child, Path.Combine(folder, "dropdown-open.png"));
			Save(window, Path.Combine(folder, "dropdown-control.png"));
			theme.IsDropDownOpen = false;
			ComboBox format = (ComboBox)window.FindName("ExportFormat");
			format.BringIntoView();
			format.SelectedIndex = 1;
			if (AppSettings.Load().ExportFormat != "SOURCE" || AppSettings.Load().OnlyBlend)
			{
				throw new InvalidOperationException("Source format was not saved.");
			}
            foreach (string choice in ExchangeExport.Formats.Skip(2))
            {
                format.SelectedIndex = Array.IndexOf(ExchangeExport.Formats, choice);
                if (AppSettings.Load().ExportFormat != choice) throw new InvalidOperationException("Export format was not saved: " + choice);
            }
            var limit = (ComboBox)window.FindName("TextureLimit");
            limit.SelectedIndex = 3;
            ((CheckBox)window.FindName("Triangulate")).IsChecked = true;
            ((CheckBox)window.FindName("IncludeAnimation")).IsChecked = false;
            var exportOptions = AppSettings.Load();
            if (exportOptions.TextureSizeLimit != 2048 || !exportOptions.TriangulateMeshes || exportOptions.ExportAnimations)
                throw new InvalidOperationException("Exchange options were not saved.");
			format.SelectedIndex = 0;
			if (AppSettings.Load().ExportFormat != "BLEND" || !AppSettings.Load().OnlyBlend)
			{
				throw new InvalidOperationException("Packed Blender format was not saved.");
			}
			format.IsDropDownOpen = true;
			await Task.Delay(200);
			Save((FrameworkElement)((Popup)format.Template.FindName("PART_Popup", format)).Child, Path.Combine(folder, "format-dropdown.png"));
			format.IsDropDownOpen = false;
			List<Cosmetic> list = new List<Cosmetic>
			{
				new Cosmetic("skin1", "Skin one", "Skins", ""),
				new Cosmetic("skin2", "Skin two", "Skins", ""),
				new Cosmetic("emote1", "Emote one", "Animations", ""),
				new Cosmetic("emote2", "Emote two", "Animations", "")
			};
			typeof(MainWindow).GetField("_cosmetics", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, list);
			typeof(MainWindow).GetMethod("RefreshFilter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
			((RadioButton)window.FindName("SceneMode")).IsChecked = true;
			ListBox listBox = (ListBox)window.FindName("SceneSkinsList");
			ListBox listBox2 = (ListBox)window.FindName("SceneEmotesList");
			listBox.SelectedItems.Add(list[0]);
			listBox.SelectedItems.Add(list[1]);
			listBox2.SelectedItems.Add(list[2]);
			listBox2.SelectedItems.Add(list[3]);
			if (listBox.SelectedItems.Count != 2 || listBox2.SelectedItems.Count != 2)
			{
				throw new InvalidOperationException("Independent multi-selection failed.");
			}
			TextBox textBox = (TextBox)window.FindName("Search");
			textBox.Text = "Emote";
			if (listBox.SelectedItems.Count != 2 || listBox2.SelectedItems.Count != 2)
			{
				throw new InvalidOperationException("Searching cleared the skin/emote scene selections.");
			}
			ComboBox comboBox = (ComboBox)window.FindName("Category");
			comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().First((ComboBoxItem x) => (string)x.Content == "Animations");
			if (listBox.SelectedItems.Count != 2 || listBox2.SelectedItems.Count != 2 || !((Button)window.FindName("ExportButton")).IsEnabled)
			{
				throw new InvalidOperationException("Switching categories lost the scene or disabled export.");
			}
			textBox.Text = "";
			List<CosmeticStyleChannel> list2 = new List<CosmeticStyleChannel>
			{
				new CosmeticStyleChannel("body", "Armour", "0", new List<CosmeticStyleOption>(2)
				{
					new CosmeticStyleOption("0", "Base"),
					new CosmeticStyleOption("5", "Stage 5")
				}),
				new CosmeticStyleChannel("light", "Lights", "0", new List<CosmeticStyleOption>(2)
				{
					new CosmeticStyleOption("0", "Red", "#FF0000"),
					new CosmeticStyleOption("2", "Purple", "#8F20FF")
				})
			};
			typeof(MainWindow).GetMethod("BuildStyleChannels", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[2]
			{
				list[0],
				list2
			});
			TabControl tabControl = (TabControl)window.FindName("StyleChannelsTab");
			foreach (TabItem item in (IEnumerable)tabControl.Items)
			{
				((ComboBox)((StackPanel)item.Content).Children[0]).SelectedIndex = 1;
			}
			Dictionary<string, string> dictionary = StyleSelection.Decode(((Dictionary<string, string>)typeof(MainWindow).GetField("_selectedStyles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window))[list[0].Id]);
			if (dictionary["body"] != "5" || dictionary["light"] != "2")
			{
				throw new Exception("Style tabs did not preserve both choices.");
			}
			tabControl.BringIntoView();
			await Task.Delay(100);
			Save(window, Path.Combine(folder, "style-tabs.png"));
			Func<AppSettings, IProgress<string>, CancellationToken, Task> func = async (AppSettings _, IProgress<string> p, CancellationToken _) =>
			{
				p.Report("Downloading archive index 25/100 bytes · test");
				await Task.Delay(100);
				ProgressBar bar = (ProgressBar)window.FindName("Activity");
				if (bar.IsIndeterminate || bar.Value != 25.0)
				{
					throw new Exception("Byte progress is not real progress.");
				}
				p.Report("Attaching Dance: 50/100 bones");
				await Task.Delay(100);
				if (bar.Value != 50.0)
				{
					throw new Exception("Animation progress is incorrect.");
				}
			};
			await (Task)typeof(MainWindow).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[1] { func });
			if (((ProgressBar)window.FindName("Activity")).Value != 100.0)
			{
				throw new Exception("Completed operation did not finish progress.");
			}
			Func<AppSettings, IProgress<string>, CancellationToken, Task> func2 = (AppSettings _, IProgress<string> p, CancellationToken _) =>
			{
				p.Report("Exporting mesh 1/2: cancelled fixture");
				return Task.FromCanceled(new CancellationToken(canceled: true));
			};
			await (Task)typeof(MainWindow).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[1] { func2 });
			await Task.Delay(100);
			if (((ProgressBar)window.FindName("Activity")).Value != 0.0 || ((TextBlock)window.FindName("ProgressText")).Text != "Cancelled")
			{
				throw new Exception("Cancellation or stale progress changed the result.");
			}
			BitmapImage thumbnail = new BitmapImage(new Uri("https://fortnite-api.com/images/cosmetics/br/cid_116_athena_commando_m_carbideblack/icon.png"));
			if (thumbnail.IsDownloading)
			{
				TaskCompletionSource done = new TaskCompletionSource();
				thumbnail.DownloadCompleted += (object? _, EventArgs _) =>
				{
					done.TrySetResult();
				};
				thumbnail.DownloadFailed += (object? _, ExceptionEventArgs e) =>
				{
					done.TrySetException(e.ErrorException);
				};
				await done.Task.WaitAsync(TimeSpan.FromSeconds(20L));
			}
			if (thumbnail.PixelWidth < 32)
			{
				throw new Exception("Live catalogue thumbnail failed to load.");
			}
			window.Width = 1220.0;
			window.Height = 760.0;
			await Task.Delay(200);
			Save(window, Path.Combine(folder, "minimum-size.png"));
			AnimatedBackdrop backdrop = (AnimatedBackdrop)window.FindName("Backdrop");
			backdrop.ReducedMotion = true;
			Save(backdrop, Path.Combine(folder, "still-1.png"));
			await Task.Delay(300);
			Save(backdrop, Path.Combine(folder, "still-2.png"));
			if (!File.ReadAllBytes(Path.Combine(folder, "still-1.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "still-2.png"))))
			{
				throw new InvalidOperationException("Reduced motion did not stop the background.");
			}
            var games = (ListBox)window.FindName("GameSelector");
            var gamePath = (TextBox)window.FindName("GamePath");
            string fortnitePath = gamePath.Text;
            games.SelectedIndex = 1;
            if (((RadioButton)window.FindName("SceneMode")).Visibility != Visibility.Collapsed || ((Button)window.FindName("ExportButton")).IsEnabled)
                throw new Exception("Game switch retained Fortnite export state.");
            var visiblePartCategories = ((ComboBox)window.FindName("Category")).Items.OfType<ComboBoxItem>()
                .Where(x => x.Visibility == Visibility.Visible && x.Content?.ToString() != "All assets")
                .Select(x => x.Content!.ToString()).Order().ToArray();
            if (!visiblePartCategories.SequenceEqual(UnitySession.PartCategories.Order()))
                throw new Exception("PCBS2 includes non-part categories or is missing a part category.");
            gamePath.Text = @"C:\Games\PCBS2-test";
            ((TabControl)window.FindName("WorkspaceTabs")).SelectedIndex = 0;
            await Task.Delay(200);
            Save(window, Path.Combine(folder, "pcbs2-minimum-size.png"));
            games.SelectedIndex = 0;
            if (gamePath.Text != fortnitePath) throw new Exception($"Game switch overwrote Fortnite path. Expected '{fortnitePath}', got '{gamePath.Text}'.");
            games.SelectedIndex = 1;
            if (gamePath.Text != @"C:\Games\PCBS2-test" || AppSettings.Load().PcBuildingFolder != gamePath.Text)
                throw new Exception("PCBS2 path was not retained independently.");
            games.SelectedIndex = 2;
            var rlCategories = ((ComboBox)window.FindName("Category")).Items.OfType<ComboBoxItem>()
                .Where(x=>x.Visibility==Visibility.Visible && x.Content?.ToString()!="All assets").Select(x=>x.Content!.ToString()).Order().ToArray();
            if(!rlCategories.SequenceEqual(RocketLeagueSession.Categories.Order())) throw new Exception("Rocket League category isolation failed.");
            gamePath.Text=@"C:\Games\RocketLeague-test";
            games.SelectedIndex=1;
            if(gamePath.Text!=@"C:\Games\PCBS2-test") throw new Exception("Rocket League overwrote PCBS2 path.");
            games.SelectedIndex=2;
            if(gamePath.Text!=@"C:\Games\RocketLeague-test" || AppSettings.Load().RocketLeagueFolder!=gamePath.Text) throw new Exception("Rocket League folder was not retained.");
            var rocketSession=(RocketLeagueSession)typeof(MainWindow).GetField("_rocketSession",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            typeof(RocketLeagueSession).GetField("_styles",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(rocketSession,new Dictionary<string,List<RocketLeagueSession.Decal>>{{"RL-test",new(){new("decal-compatible","Compatible stripes","fixture.upk")}}});
            var rocketCatalog=new List<Cosmetic>{new("RL-test","Test car","Cars","fixture.upk")};
            typeof(MainWindow).GetField("_cosmetics",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,rocketCatalog);
            typeof(MainWindow).GetMethod("RefreshFilter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            ((DataGrid)window.FindName("CosmeticsGrid")).SelectedItem=rocketCatalog[0];
            var rocketStyles=(ComboBox)window.FindName("StyleSelector");
            if(rocketStyles.Items.Count!=2 || !rocketStyles.IsEnabled)throw new Exception("Compatible decal styles did not load for the selected car.");
            rocketStyles.SelectedIndex=1;
            var storedStyles=(Dictionary<string,string>)typeof(MainWindow).GetField("_selectedStyles",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            if(storedStyles.GetValueOrDefault("RL-test")!="decal-compatible")throw new Exception("Selected Rocket League decal was not saved for export.");
            var appearances=(Dictionary<string,RocketAppearance>)typeof(MainWindow).GetField("_rocketAppearances",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var appearancePanel=(StackPanel)window.FindName("RocketAppearancePanel");
            var colourCombos=appearancePanel.Children.OfType<ComboBox>().Where(c=>c.Items.OfType<CosmeticStyleOption>().Any()).ToArray();
            if(colourCombos.Length!=2)throw new Exception("Independent car/decal colour controls missing.");
            colourCombos[0].SelectedItem=colourCombos[0].Items.OfType<CosmeticStyleOption>().Single(o=>o.Name=="Cobalt");
            colourCombos[1].SelectedItem=colourCombos[1].Items.OfType<CosmeticStyleOption>().Single(o=>o.Name=="Crimson");
            if(appearances["RL-test"].Primary!="#264DD9" || appearances["RL-test"].Secondary!="#B7162D" || appearances["RL-test"].DecalId!="decal-compatible")throw new Exception("Car colour, decal colour or decal style overwrote another channel.");
            if(!appearancePanel.Children.OfType<Button>().Any(b=>b.Content?.ToString()=="Generate preview"))throw new Exception("Appearance preview control missing.");
            if(AppSettings.Load().RocketAppearances["RL-test"].Primary!="#264DD9")throw new Exception("Rocket League appearance was not saved.");
            Save(window,Path.Combine(folder,"rocketleague-categories.png"));
            games.SelectedIndex = 0;
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
            var scanMethod = typeof(MainWindow).GetMethod("ScanLibrary", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishScan = new TaskCompletionSource<List<Cosmetic>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<AppSettings, IProgress<string>, CancellationToken, Task<List<Cosmetic>>> scan = async (_, progress, ct) =>
            {
                progress.Report("Reading cosmetics 25/100");
                started.SetResult();
                return await finishScan.Task.WaitAsync(ct);
            };
            Task runningScan = (Task)scanMethod.Invoke(window, new object[] { true, scan })!;
            await started.Task;
            await Task.Delay(100);
            if (!games.IsEnabled || !((TextBox)window.FindName("Search")).IsEnabled || !((Grid)window.FindName("SettingsPanel")).IsEnabled)
                throw new Exception("Background scan disables browsing or settings.");
            if (((Button)window.FindName("ScanButton")).IsEnabled || ((Button)window.FindName("ExportButton")).IsEnabled)
                throw new Exception("Background scan allows conflicting operations.");
            ((TabControl)window.FindName("WorkspaceTabs")).SelectedIndex = 1;
            games.SelectedIndex = 1;
            if (((ProgressBar)window.FindName("Activity")).Value != 25)
                throw new Exception("Switching games resets active progress.");
            ((TextBox)window.FindName("Search")).Text = "GPU";
            ((TabControl)window.FindName("WorkspaceTabs")).SelectedIndex = 0;
            Save(window, Path.Combine(folder, "scan-navigation.png"));
            finishScan.SetResult(new List<Cosmetic> { new("scan-test", "Scan result", "Skins", "") });
            await runningScan;
            if (((DataGrid)window.FindName("CosmeticsGrid")).Items.OfType<Cosmetic>().Any(x => x.Id == "scan-test"))
                throw new Exception("Fortnite scan results leaked into PCBS2.");
            games.SelectedIndex = 0;
            if (!((DataGrid)window.FindName("CosmeticsGrid")).Items.OfType<Cosmetic>().Any(x => x.Id == "scan-test"))
                throw new Exception("Completed scan was lost while viewing another game.");
            games.SelectedIndex = 1; games.SelectedIndex = 0;
            if (((DataGrid)window.FindName("CosmeticsGrid")).Items.Count != 1)
                throw new Exception("Game switch lost cached catalogue.");
            var cancelledScan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<AppSettings, IProgress<string>, CancellationToken, Task<List<Cosmetic>>> cancellable = async (_, _, ct) =>
            {
                cancelledScan.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new();
            };
            runningScan = (Task)scanMethod.Invoke(window, new object[] { true, cancellable })!;
            await cancelledScan.Task;
            ((Button)window.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await runningScan;
            if (!((Button)window.FindName("ScanButton")).IsEnabled || ((TextBlock)window.FindName("ProgressText")).Text != "Cancelled")
                throw new Exception("Cancelling a background scan did not restore controls.");
            var websiteLookup = typeof(MainWindow).GetMethod("FindWebsiteAsset", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var cached = (Dictionary<int, List<Cosmetic>>)typeof(MainWindow).GetField("_gameCatalogs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            cached[1] = new() { new("0123456789abcdefabcd", "Exact PC part", "GPUs", ""), new("1123456789abcdefabcd", "Other PC part", "GPUs", "") };
            cached[0] = new() { new("skin-id", "Exact skin", "Skins", ""), new("skin-id-extra", "Similar skin", "Skins", "") };
            typeof(MainWindow).GetField("_cosmetics", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, cached[0]);
            var websiteField = (TextBox)window.FindName("WebsiteId");
            websiteField.Text = " pcbs2:0123456789abcdefabcd ";
            await (Task)websiteLookup.Invoke(window, new object[] { false })!;
            var websiteGrid = (DataGrid)window.FindName("CosmeticsGrid");
            if (games.SelectedIndex != 1 || websiteGrid.Items.Count != 1 || (websiteGrid.SelectedItem as Cosmetic)?.Name != "Exact PC part")
                throw new Exception("Website ID did not select exact PC part across games.");
            ((RadioButton)window.FindName("SceneMode")).IsChecked = true;
            websiteField.Text = "fn:skin-id";
            await (Task)websiteLookup.Invoke(window, new object[] { false })!;
            if (games.SelectedIndex != 0 || websiteGrid.Items.Count != 1 || (websiteGrid.SelectedItem as Cosmetic)?.Id != "skin-id" || ((RadioButton)window.FindName("SingleMode")).IsChecked != true)
                throw new Exception("Website ID matched a substring or left scene mode selected.");
            using (var registryStream = typeof(WebsiteAssetId).Assembly.GetManifestResourceStream("website-id-registry.json")!)
            using (var registryJson = JsonDocument.Parse(registryStream))
            {
                var rows = registryJson.RootElement.GetProperty("assets").EnumerateArray().ToArray();
                foreach (bool fortniteGame in new[] { true, false })
                {
                    var row = rows.First(x => x.GetProperty("source").GetString()!.StartsWith(fortniteGame ? "fn:" : "pcbs2:"));
                    string nativeId = row.GetProperty("assetId").GetString()!;
                    games.SelectedIndex = fortniteGame ? 1 : 0;
                    cached[fortniteGame ? 0 : 1] = new() { new(nativeId, "Registered exact asset", fortniteGame ? "Skins" : "GPUs", ""), new(nativeId + "-extra", "Similar registered asset", fortniteGame ? "Skins" : "GPUs", "") };
                    websiteField.Text = row.GetProperty("id").GetString()!.ToLowerInvariant();
                    await (Task)websiteLookup.Invoke(window, new object[] { false })!;
                    if (games.SelectedIndex != (fortniteGame ? 0 : 1) || websiteGrid.Items.Count != 1 || (websiteGrid.SelectedItem as Cosmetic)?.Id != nativeId)
                        throw new Exception("Short website ID did not resolve exactly across games.");
                }
            }
            websiteField.Text = "fn:missing-id";
            await (Task)websiteLookup.Invoke(window, new object[] { false })!;
            if (!((TextBlock)window.FindName("Status")).Text.Contains("not in your scanned")) throw new Exception("Missing ID was not reported.");
            if (WebsiteAssetId.TryParse("pcbs2:../../file", out _) || WebsiteAssetId.TryParse("fn:", out _) || WebsiteAssetId.TryParse("other:skin-id", out _))
                throw new Exception("Malformed website IDs accepted.");
            Save(window, Path.Combine(folder, "website-id.png"));
            if (window.WindowStyle != WindowStyle.None || chrome?.CaptionHeight != 78)
                throw new Exception("Custom caption or native drag region is missing.");
            var maximizeButton = (Button)window.FindName("MaximizeButton");
            if (!System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(maximizeButton))
                throw new Exception("Caption controls cannot receive mouse input.");
            maximizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Task.Delay(150);
            if (window.WindowState != WindowState.Maximized || maximizeButton.ToolTip?.ToString() != "Restore")
                throw new Exception("Maximize button failed.");
            maximizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Task.Delay(150);
            if (window.WindowState != WindowState.Normal) throw new Exception("Restore button failed.");
            ((Button)window.FindName("MinimizeButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Task.Delay(150);
            if (window.WindowState != WindowState.Minimized) throw new Exception("Minimize button failed.");
            window.WindowState = WindowState.Normal;
            await Task.Delay(150);
            Save(window, Path.Combine(folder, "custom-titlebar.png"));
            ((Button)window.FindName("CloseWindowButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Task.Delay(100);
            if (window.IsVisible) throw new Exception("Close button failed.");
			File.WriteAllText(Path.Combine(folder, "result.json"), JsonSerializer.Serialize(new
			{
				CustomTitlebarControls = true,
                ArchiveIndexUiThreadSafe = true,
                WebsiteExactIdLookup = true,
                WebsiteShortIdsAcrossGames = true,
                BackgroundScanNavigation = true,
                ScanResultsStayWithGame = true,
                ScanCancellation = true,
                PcPartsOnly = true,
                GameNavigation = true,
                SeparateGamePaths = true,
                Themes = 3,
				MotionChanges = true,
				ReducedMotion = true,
				DropdownRendered = true,
				ManualUpdateNotice = true,
				UpdatePreferenceSaved = true,
				IndependentMultiSelection = true,
				MinimumWindowSize = true,
				IndependentStyleChannels = true,
				DownloadProgress = true,
				AnimationProgress = true,
				CompletionProgress = true,
				CancellationProgress = true,
				StaleProgressIgnored = true,
				LiveThumbnail = true
			}));
		}
		finally
		{
			if (window.IsLoaded) window.Close();
			if (settings != null)
			{
				File.WriteAllBytes(settingsPath, settings);
			}
			if (previousLog != null)
			{
				File.WriteAllBytes(logPath, previousLog);
			}
		}
	}

	private static void Save(FrameworkElement element, string path)
	{
		element.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(element);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}
}
