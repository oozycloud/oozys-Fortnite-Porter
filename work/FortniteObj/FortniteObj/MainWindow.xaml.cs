using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using FortniteObj.Core;
using Microsoft.Win32;

namespace FortniteObj;

public partial class MainWindow : Window, IComponentConnector
{
	private sealed record StyleChoice(string Name, string? ImageUrl)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	public static readonly DependencyProperty DropdownAnimationProperty = DependencyProperty.Register("DropdownAnimation", typeof(PopupAnimation), typeof(MainWindow), new PropertyMetadata(PopupAnimation.Fade));

	private readonly GameSession _session = new GameSession();

	private List<Cosmetic> _cosmetics = new List<Cosmetic>();

	private List<Cosmetic>? _sceneCatalog;

	private CancellationTokenSource? _operation;

	private bool _loaded;

	private string? _lastExport;

	private Cosmetic? _sceneSkin;

	private Cosmetic? _sceneEmote;

	private IReadOnlyList<Cosmetic> _selectedSkins = Array.Empty<Cosmetic>();

	private IReadOnlyList<Cosmetic> _selectedEmotes = Array.Empty<Cosmetic>();

	private Cosmetic? _styleAsset;

	private readonly Dictionary<string, string> _selectedStyles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private bool _syncingSceneSelections;

	private bool _updatingStyles;

	private bool _scanning;

	private readonly string _logFile = Path.Combine(AppSettings.DataFolder, "last-session.log");

	private readonly HttpClient _updateHttp;

	private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

	private readonly bool _startupUpdates;

	private bool _checkingUpdates;

	private bool _updatingEmoteStyles;

	private string? _releaseUrl;

	public PopupAnimation DropdownAnimation
	{
		get
		{
			return (PopupAnimation)GetValue(DropdownAnimationProperty);
		}
		set
		{
			SetValue(DropdownAnimationProperty, value);
		}
	}

	public MainWindow()
		: this(checkUpdates: true)
	{
	}

	public MainWindow(bool checkUpdates, HttpClient? updateHttp = null)
	{
		_updateHttp = updateHttp ?? new HttpClient();
		_startupUpdates = checkUpdates;
		InitializeComponent();
		Directory.CreateDirectory(AppSettings.DataFolder);
		File.WriteAllText(_logFile, $"oozy's Fortnite Porter — {DateTime.Now}\n");
		AppSettings appSettings = AppSettings.Load();
		GamePath.Text = ((!string.IsNullOrWhiteSpace(appSettings.GameFolder)) ? appSettings.GameFolder : (Installation.Detect()?.Path ?? ""));
		ExportPath.Text = appSettings.ExportFolder;
		EngineVersion.ItemsSource = new string[11]
		{
			"GAME_UE5_9", "GAME_UE5_8", "GAME_UE5_7", "GAME_UE5_6", "GAME_UE5_5", "GAME_UE5_4", "GAME_UE5_3", "GAME_UE5_2", "GAME_UE5_1", "GAME_UE5_0",
			"GAME_UE4_27"
		};
		EngineVersion.SelectedItem = appSettings.EngineVersion;
		MappingPath.Text = appSettings.MappingFile;
		MainKey.Text = appSettings.MainKey;
		AutoCompatibility.IsChecked = appSettings.AutoCompatibility;
		ExportFormat.SelectedIndex = Math.Max(0, Array.IndexOf(ExchangeExport.Formats, appSettings.ExportFormat));
        IncludeAnimation.IsChecked = appSettings.ExportAnimations;
        Triangulate.IsChecked = appSettings.TriangulateMeshes;
        TextureLimit.SelectedIndex = Math.Max(0, Array.IndexOf(new[] {0,512,1024,2048,4096}, appSettings.TextureSizeLimit));
		MatteMaterials.IsChecked = appSettings.MatteMaterials;
		ReduceMotion.IsChecked = appSettings.ReduceMotion;
		AutoUpdateCheck.IsChecked = appSettings.CheckForUpdatesAutomatically;
		AnimatedTheme.ItemsSource = new string[4] { "Embers", "Orbit", "Rain", "Still" };
		AnimatedTheme.SelectedItem = appSettings.AnimatedTheme;
		if (AnimatedTheme.SelectedIndex < 0)
		{
			AnimatedTheme.SelectedIndex = 0;
		}
		AnimationFps.Text = ((appSettings.AnimationFps > 0.0) ? appSettings.AnimationFps.ToString("0.###", CultureInfo.InvariantCulture) : "");
		StyleSelector.ItemsSource = new StyleChoice[1]
		{
			new StyleChoice("Default", null)
		};
		StyleSelector.SelectedIndex = 0;
		SceneName.Text = "Fortnite_Scene";
        _fortniteFolder = GamePath.Text;
        _pcBuildingFolder = appSettings.PcBuildingFolder;
        _rocketLeagueFolder = appSettings.RocketLeagueFolder;
        GameSelector.SelectedIndex = appSettings.SelectedGame == "RocketLeague" ? 2 : appSettings.SelectedGame == "PCBS2" ? 1 : 0;
        _loaded = true;
        ApplyGameSelection();
		UpdateMotionPreferences();
		UpdateMaterialHint();
		UpdateSceneLabels();
	}

	private AppSettings ReadSettings()
	{
		return new AppSettings
		{
			GameFolder = IsFortnite ? GamePath.Text : _fortniteFolder,
            PcBuildingFolder = _activeGame == 1 ? GamePath.Text : _pcBuildingFolder,
            RocketLeagueFolder = IsRocketLeague ? GamePath.Text : _rocketLeagueFolder,
            SelectedGame = IsFortnite ? "Fortnite" : IsRocketLeague ? "RocketLeague" : "PCBS2",
			ExportFolder = ExportPath.Text,
			EngineVersion = ((EngineVersion.SelectedItem as string) ?? "GAME_UE5_8"),
			MappingFile = MappingPath.Text.Trim(),
			MainKey = MainKey.Text.Trim(),
			AutoCompatibility = (AutoCompatibility.IsChecked == true),
			OnlyBlend = (ExportFormat.SelectedIndex != 1),
			ExportFormat = ExchangeExport.Formats[Math.Max(0, ExportFormat.SelectedIndex)],
            ExportAnimations = IncludeAnimation.IsChecked == true,
            TriangulateMeshes = Triangulate.IsChecked == true,
            TextureSizeLimit = new[] {0,512,1024,2048,4096}[Math.Max(0, TextureLimit.SelectedIndex)],
			MatteMaterials = (MatteMaterials.IsChecked == true),
			ReduceMotion = (ReduceMotion.IsChecked == true),
			CheckForUpdatesAutomatically = (AutoUpdateCheck.IsChecked == true),
			AnimatedTheme = ((AnimatedTheme.SelectedItem as string) ?? "Embers"),
			AnimationFps = ReadAnimationFps(),
			DownloadHighResolution = true
		};
	}

	private double ReadAnimationFps()
	{
		string text = AnimationFps.Text.Trim();
		if (text.Length == 0)
		{
			return 0.0;
		}
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result) && result >= 1.0 && result <= 240.0)
		{
			return result;
		}
		throw new ArgumentException("Animation FPS must be between 1 and 240. Leave it blank to use the source rate.");
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		SystemParameters.StaticPropertyChanged += SystemPreferences_Changed;
		StateChanged += (object? _, EventArgs _) =>
		{
            UpdateCaptionButtons();
			UpdateMotionPreferences();
		};
		UiMotion.Reveal(HeaderPanel, 0, 5.0);
		UiMotion.Reveal(LibraryShell, 15);
		UiMotion.Reveal(CatalogPanel, 40);
		UiMotion.Reveal(InspectorShell, 65);
        if (_startupUpdates)
            await Task.WhenAll(RefreshCompatibilityAtLaunch(), AutoUpdateCheck.IsChecked == true ? CheckUpdates() : Task.CompletedTask);
	}

	private void AutoUpdate_Changed(object sender, RoutedEventArgs e)
	{
		if (_loaded)
		{
			SaveAppearanceSettings();
		}
	}

	private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
	{
		await CheckUpdates();
	}

	private async Task CheckUpdates()
	{
		if (_checkingUpdates)
		{
			return;
		}
		_checkingUpdates = true;
		CheckUpdateButton.IsEnabled = false;
		UpdateStatus.Text = "Checking GitHub…";
		try
		{
			UpdateResult updateResult = await new UpdateChecker(_updateHttp).Check(_lifetime.Token);
			UpdateStatus.Text = updateResult.Message;
			_releaseUrl = updateResult.ReleaseUrl;
			Button releaseButton = ReleaseButton;
			Visibility visibility = (UpdateNotice.Visibility = ((!updateResult.Available) ? Visibility.Collapsed : Visibility.Visible));
			releaseButton.Visibility = visibility;
			UpdateNotice.Content = (updateResult.Available ? ("Update available · " + updateResult.Version) : "");
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			_checkingUpdates = false;
			CheckUpdateButton.IsEnabled = true;
		}
	}

	private void OpenRelease_Click(object sender, RoutedEventArgs e)
	{
		if (_releaseUrl != null)
		{
			try
			{
				Process.Start(new ProcessStartInfo(_releaseUrl)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				UpdateStatus.Text = "Could not open your browser: " + ex.Message;
			}
		}
	}

	private void Format_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (OutputFormatHint != null)
		{
			OutputFormatHint.Text = IsFortnite ? ExchangeExport.Hint(ExchangeExport.Formats[Math.Max(0, ExportFormat.SelectedIndex)]) : IsRocketLeague ? "Rocket League meshes use the selected format. Texture-only effects retain extracted source resources." : "PC part models use the selected format. Blender retains their hierarchy and available base-colour textures.";
		}
		if (_loaded)
		{
			SaveAppearanceSettings();
		}
	}

	private async void InstallBlenderTools_Click(object sender, RoutedEventArgs e)
	{
		await Run(async (AppSettings _, IProgress<string> progress, CancellationToken ct) =>
		{
			MessageBox.Show(this, "Emote tools installed for " + Path.GetFileName(Path.GetDirectoryName(await Task.Run(() => BlenderTools.Install(progress, ct), ct))) + ".\n\nRestart Blender, open your exported skin, select its mesh or rig, and press N in the 3D View. Under Porter, choose Apply exported emote and select the emote's .blend file.", "Blender tools", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		});
	}

	private void WorkspaceTab_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_loaded && e.OriginalSource == WorkspaceTabs)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)(() =>
			{
				UiMotion.Reveal((WorkspaceTabs.SelectedIndex == 0) ? LibraryTabContent : SettingsTabContent);
			}));
		}
	}

	private void UpdateMotionPreferences()
	{
		bool flag = ReduceMotion.IsChecked == true || !SystemParameters.ClientAreaAnimation;
		UiMotion.SetReduceMotion(this, flag);
		Backdrop.ReducedMotion = flag || WindowState == WindowState.Minimized;
		Backdrop.Theme = (AnimatedTheme.SelectedItem as string) ?? "Embers";
		DropdownAnimation = ((!flag) ? PopupAnimation.Fade : PopupAnimation.None);
		if (flag)
		{
			UiMotion.Reset(this);
		}
		if (flag)
		{
			Activity.IsIndeterminate = false;
		}
	}

	private void Theme_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_loaded)
		{
			UpdateMotionPreferences();
			SaveAppearanceSettings();
		}
	}

	private void SystemPreferences_Changed(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ClientAreaAnimation")
		{
			Dispatcher.BeginInvoke(new Action(UpdateMotionPreferences));
		}
	}

	private void MotionPreference_Changed(object sender, RoutedEventArgs e)
	{
		if (_loaded)
		{
			UpdateMotionPreferences();
			SaveAppearanceSettings();
		}
	}

	private void Appearance_Changed(object sender, RoutedEventArgs e)
	{
		if (_loaded)
		{
			UpdateMaterialHint();
			SaveAppearanceSettings();
		}
	}

	private void UpdateMaterialHint()
	{
		MaterialLookHint.Text = ((MatteMaterials.IsChecked == true) ? "Matte materials" : "Source materials");
	}

	private void SaveAppearanceSettings()
	{
		try
		{
			ReadSettings().Save();
		}
		catch (Exception ex)
		{
			Report("Could not save preferences: " + ex.Message);
		}
	}

	private void Report(string message)
	{
		Status.Text = message;
		if (_operation != null)
		{
			Match match = Regex.Match(message, "^(?:Reading Rocket League balls |Reading Unity files |Reading cosmetics |Exporting mesh |Downloading archive index |Attaching .*?: )(\\d[\\d,]*)/(\\d[\\d,]*)");
			if (match.Success && double.TryParse(match.Groups[1].Value.Replace(",", ""), out var result) && double.TryParse(match.Groups[2].Value.Replace(",", ""), out var result2) && result2 > 0.0)
			{
				Activity.IsIndeterminate = false;
				Activity.Value = Math.Clamp(result / result2 * 100.0, 0.0, 100.0);
				ProgressText.Text = $"{Activity.Value:0}% of current stage";
			}
			else
			{
				Activity.Value = 0.0;
				Activity.IsIndeterminate = ReduceMotion.IsChecked != true && SystemParameters.ClientAreaAnimation;
				ProgressText.Text = "Working…";
			}
		}
		File.AppendAllText(_logFile, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
	}

	private async Task Run(Func<AppSettings, IProgress<string>, CancellationToken, Task> action)
	{
		if (_operation != null)
		{
			return;
		}
		_operation = new CancellationTokenSource();
		CancellationTokenSource operation = _operation;
		Activity.Value = 0.0;
		Activity.IsIndeterminate = ReduceMotion.IsChecked != true;
		ProgressText.Text = "Starting…";
		SetBusy(busy: true);
		try
		{
			AppSettings appSettings = ReadSettings();
			appSettings.Save();
			await action(appSettings, new Progress<string>((string message) =>
			{
				if (_operation == operation)
				{
					Report(message);
				}
			}), operation.Token);
			Activity.IsIndeterminate = false;
			Activity.Value = 100.0;
			ProgressText.Text = "Complete";
		}
		catch (OperationCanceledException)
		{
			Report("Cancelled. You can start another operation.");
			Activity.IsIndeterminate = false;
			Activity.Value = 0.0;
			ProgressText.Text = "Cancelled";
		}
		catch (Exception ex2)
		{
			Report("Could not finish: " + ex2.Message);
			File.AppendAllText(_logFile, ex2?.ToString() + "\n");
			MessageBox.Show(this, ex2.Message, "oozy's Fortnite Porter", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			Activity.IsIndeterminate = false;
			Activity.Value = 0.0;
			ProgressText.Text = "Failed — see session log";
		}
		finally
		{
			_operation.Dispose();
			_operation = null;
			SetBusy(busy: false);
		}
	}

	private void SetBusy(bool busy)
	{
		SettingsPanel.IsEnabled = !busy || _scanning;
		CatalogPanel.IsEnabled = !busy || _scanning;
		InspectorPanel.IsEnabled = !busy || _scanning;
		OpenFolderButton.IsEnabled = true;
        ScanButton.IsEnabled = !busy;
        FindWebsiteIdButton.IsEnabled = ExportWebsiteIdButton.IsEnabled = !busy && !_findingWebsiteId;
		UpdateMotionPreferences();
		StatusDot.Fill = new SolidColorBrush(busy ? Color.FromRgb(246, 183, 125) : Color.FromRgb(99, 216, 234));
		CancelButton.Visibility = ((!busy) ? Visibility.Collapsed : Visibility.Visible);
		ExportButton.IsEnabled = !busy && CanExport();
		if (busy)
		{
			UiMotion.Reveal(CancelButton, 0, 3.0);
		}
		else
		{
			UiMotion.Reveal(Status, 0, 0.0);
		}
	}

	private async void Scan_Click(object sender, RoutedEventArgs e)
	{
        int game = _activeGame;
        await ScanGameLibrary(game, (settings, progress, ct) => game == 0 ? _session.Scan(settings, progress, ct) : game == 2 ? _rocketSession.Scan(settings.RocketLeagueFolder, progress, ct) : _unitySession.Scan(settings.PcBuildingFolder, progress, ct));
    }

    private Task ScanLibrary(bool fortnite, Func<AppSettings, IProgress<string>, CancellationToken, Task<List<Cosmetic>>> load) => ScanGameLibrary(fortnite ? 0 : 1, load);

    private async Task ScanGameLibrary(int game, Func<AppSettings, IProgress<string>, CancellationToken, Task<List<Cosmetic>>> load)
    {
        bool fortnite = game == 0;
        _scanGame = game;
        if (_operation != null) return;
        _scanning = true;
        _scanFortnite = fortnite;
        try
        {
		await Run(async (AppSettings settings, IProgress<string> progress, CancellationToken ct) =>
		{
			_sceneSkin = null;
			_sceneEmote = null;
			_selectedSkins = Array.Empty<Cosmetic>();
			_selectedEmotes = Array.Empty<Cosmetic>();
			_styleAsset = null;
			UpdateSceneLabels();
			RefreshFilter();
			RefreshStyles(null);
			try
			{
                var catalog = await Task.Run(() => load(settings, progress, ct), ct);
                ct.ThrowIfCancellationRequested();
                _gameCatalogs[game] = catalog;
                if (_activeGame == game) _cosmetics = catalog;
				foreach (string warning in fortnite ? (IEnumerable<string>)_session.Warnings : game == 2 ? _rocketSession.Warnings : _unitySession.Warnings)
				{
					File.AppendAllText(_logFile, "WARNING: " + warning + "\n");
				}
			}
            catch
            {
                // A failed rescan may have disposed the old archive provider.
                _gameCatalogs[game] = new List<Cosmetic>();
                if (_activeGame == game) _cosmetics = _gameCatalogs[game];
                throw;
            }
			finally
			{
				_scanning = false;
				RefreshFilter();
                _styleAsset = null;
                RefreshStyles(CosmeticsGrid.SelectedItem as Cosmetic);
			}
		});
        }
        finally { _scanning = false; SetBusy(_operation != null); }
	}

	private async void Export_Click(object sender, RoutedEventArgs e)
	{
		if (!IsFortnite) { await ExportUnitySelection(); return; }
		bool sceneMode = SceneMode.IsChecked == true;
		Cosmetic[] skins = _selectedSkins.ToArray();
		Cosmetic[] emotes = _selectedEmotes.ToArray();
		Cosmetic[] assets = CosmeticsGrid.SelectedItems.OfType<Cosmetic>().ToArray();
		Dictionary<string, string> styles;
		if (sceneMode && (skins.Length == 0 || emotes.Length == 0))
		{
			MessageBox.Show(this, "Choose at least one skin and one emote in the separate lists.", "Scene setup", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		else
		{
			if (!sceneMode && assets.Length == 0)
			{
				return;
			}
			string sceneName = SceneName.Text.Trim();
			bool savePng = SavePng.IsChecked == true;
			styles = new Dictionary<string, string>(_selectedStyles, StringComparer.OrdinalIgnoreCase);
			await Run(async (AppSettings settings, IProgress<string> progress, CancellationToken ct) =>
			{
				List<ExportResult> results = new List<ExportResult>();
				if (sceneMode)
				{
					Cosmetic[] array = skins;
					foreach (Cosmetic skin in array)
					{
						Cosmetic[] array2 = emotes;
						foreach (Cosmetic emote in array2)
						{
							ct.ThrowIfCancellationRequested();
							List<ExportResult> list = results;
							list.Add(await Task.Run(() => _session.ExportScene(skin, emote, settings.ExportFolder, sceneName, progress, ct, StyleFor(skin), settings.OnlyBlend, settings.MatteMaterials, settings.AnimationFps, StyleFor(emote)), ct));
							if (savePng)
							{
								List<ExportResult> list2 = results;
								list = list2;
								int index = list2.Count - 1;
								list[index] = await Task.Run(() =>
								{
									GameSession session = _session;
									Cosmetic cosmetic = skin;
									string style = StyleFor(skin);
									List<ExportResult> list4 = results;
									return session.AddPreviewPng(cosmetic, style, list4[list4.Count - 1], ct);
								}, ct);
							}
						}
					}
				}
				else
				{
					Cosmetic[] array = assets;
					foreach (Cosmetic asset in array)
					{
						ct.ThrowIfCancellationRequested();
						List<ExportResult> list = results;
						list.Add(await Task.Run(() => _session.Export(asset, settings.ExportFolder, progress, ct, StyleFor(asset), settings.OnlyBlend, settings.MatteMaterials, settings.AnimationFps), ct));
						if (savePng)
						{
							List<ExportResult> list2 = results;
							list = list2;
							int j = list2.Count - 1;
							list[j] = await Task.Run(() =>
							{
								GameSession session = _session;
								Cosmetic cosmetic = asset;
								string style = StyleFor(asset);
								List<ExportResult> list4 = results;
								return session.AddPreviewPng(cosmetic, style, list4[list4.Count - 1], ct);
							}, ct);
						}
					}
				}
				for (int i = 0; i < results.Count; i++)
                    results[i] = await ExchangeExport.Create(results[i], settings, progress, ct);
                List<ExportResult> list3 = results;
				ExportResult exportResult = list3[list3.Count - 1];
				_lastExport = exportResult.Folder;
				Report(sceneMode ? $"Exported {results.Count} scene combination(s)." : $"Exported {results.Count} asset(s).");
				foreach (string item in results.SelectMany((ExportResult x) => x.Warnings))
				{
					File.AppendAllText(_logFile, "WARNING: " + item + "\n");
				}
				bool flag = Directory.EnumerateFiles(exportResult.Folder, "*.blend", SearchOption.AllDirectories).Any();
				string text = ((!flag) ? "No Blender file was produced. Check the export report and session log for details." : (sceneMode ? "Open a scene .blend file and press Space in Blender to play the emote on its skin." : (assets.Any((Cosmetic x) => x.Category == "Animations") ? "In Blender, select your exported skin, open the Porter sidebar and choose Apply exported emote. Select this emote's .blend file. Install the Blender tools from Settings first; Ctrl+P is not needed." : (assets.Any((Cosmetic x) => x.Category == "Skyboxes") ? "Open the Blender skybox scene and use it as a backdrop." : ((assets.Any((Cosmetic x) => x.Category == "Pickaxes") && File.Exists(Path.Combine(exportResult.Folder, "fx-report.json"))) ? "Open the .blend file and scrub frames 1–24 to preview the pickaxe effects." : "Open the .blend file to view textures and pose the rig. In Pose Mode, select a bone and press R. For cloth pins and attachment points, open Physics setup.txt in Blender's Text Editor.")))));
				int num = results.Sum((ExportResult x) => x.Warnings.Length);
				if (num > 0)
				{
					text += $"\n\n{num} export warning(s). Open the session log for details.";
				}
				if (settings.ExportFormat is not ("BLEND" or "SOURCE")) text = ExchangeExport.Hint(settings.ExportFormat) + "\n\nFind the exported files in the " + settings.ExportFormat + " subfolder.";
                MessageBox.Show(this, $"Saved {results.Count} export(s). Last folder:\n{exportResult.Folder}\n\n" + text, flag ? "Export complete" : "Export needs attention", MessageBoxButton.OK, flag ? MessageBoxImage.Asterisk : MessageBoxImage.Exclamation);
			});
		}
		string StyleFor(Cosmetic asset)
		{
			return styles.GetValueOrDefault(asset.Id, "Default");
		}
	}

	private void RefreshFilter()
	{
		if (!_loaded)
		{
			return;
		}
		string category = (Category.SelectedItem as ComboBoxItem)?.Content?.ToString();
		string query = Search.Text.Trim();
        bool websiteQuery = WebsiteAssetId.TryParse(query, out var websiteId);
		List<Cosmetic> list = _cosmetics.Where((Cosmetic x) => (category == "All assets" || x.Category == category) && (websiteQuery
            ? !IsRocketLeague && websiteId.Fortnite == IsFortnite && x.Id.Equals(websiteId.Id, StringComparison.OrdinalIgnoreCase)
            : x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || x.ObjectPath.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
		HashSet<Cosmetic> hashSet = CosmeticsGrid.SelectedItems.OfType<Cosmetic>().ToHashSet();
		CosmeticsGrid.ItemsSource = list;
		foreach (Cosmetic item in list.Where(hashSet.Contains))
		{
			CosmeticsGrid.SelectedItems.Add(item);
		}
		if (_sceneCatalog != _cosmetics)
		{
			_sceneCatalog = _cosmetics;
			SceneSkinsList.ItemsSource = _cosmetics.Where((Cosmetic x) => x.Category.Equals("Skins", StringComparison.OrdinalIgnoreCase)).ToList();
			SceneEmotesList.ItemsSource = _cosmetics.Where((Cosmetic x) => x.Category.Equals("Animations", StringComparison.OrdinalIgnoreCase)).ToList();
		}
		CountLabel.Text = $"{list.Count:N0} shown / {_cosmetics.Count:N0} assets";
		EmptyState.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		TextBlock emptyTitle = EmptyTitle;
		string text;
		if (_scanning && _scanGame == _activeGame)
		{
			text = "Reading your archives…";
		}
		else
		{
			text = ((_cosmetics.Count == 0) ? "No assets loaded" : "No matching assets");
		}
		emptyTitle.Text = text;
		TextBlock emptyDescription = EmptyDescription;
		string text2;
		if (_scanning && _scanGame == _activeGame)
		{
			text2 = "Building the asset catalog. Progress appears below; you can cancel at any time.";
		}
		else
		{
			text2 = ((_cosmetics.Count == 0) ? (IsFortnite ? "Connect your Fortnite folder, then scan to browse skins, emotes and worlds." : IsRocketLeague ? "Choose Rocket League, then scan its installed asset packages." : "Choose PC Building Simulator 2, then scan its PC part models.") : "Try another name, ID, or category.");
		}
		emptyDescription.Text = text2;
	}

	private void Filter_Changed(object sender, TextChangedEventArgs e)
	{
		RefreshFilter();
	}

	private void Category_Changed(object sender, SelectionChangedEventArgs e)
	{
		RefreshFilter();
		if (_loaded)
		{
			UiMotion.Reveal(CountLabel, 0, 3.0);
		}
	}

	private void Cosmetics_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loaded)
		{
			return;
		}
		Cosmetic[] array = CosmeticsGrid.SelectedItems.OfType<Cosmetic>().ToArray();
		Cosmetic cosmetic = array.LastOrDefault();
		TextBlock selectionTitle = SelectionTitle;
		string text;
		if (array.Length != 0)
		{
			text = ((array.Length == 1) ? cosmetic.Name : $"{array.Length:N0} assets selected");
		}
		else
		{
			text = "Nothing selected";
		}
		selectionTitle.Text = text;
		TextBlock assetSource = AssetSource;
		string text2;
		if ((object)cosmetic == null)
		{
			text2 = "Choose one or more items from the library.";
		}
		else
		{
			text2 = ((array.Length == 1) ? (cosmetic.Category + " · " + cosmetic.Availability) : "Multiple assets are ready to export.");
		}
		assetSource.Text = text2;
		if (SceneMode.IsChecked == true)
		{
			if (array.Any((Cosmetic x) => x.Category.Equals("Skins", StringComparison.OrdinalIgnoreCase)))
			{
				SetSceneListSelection(SceneSkinsList, array.Where((Cosmetic x) => x.Category.Equals("Skins", StringComparison.OrdinalIgnoreCase)));
			}
			if (array.Any((Cosmetic x) => x.Category.Equals("Animations", StringComparison.OrdinalIgnoreCase)))
			{
				SetSceneListSelection(SceneEmotesList, array.Where((Cosmetic x) => x.Category.Equals("Animations", StringComparison.OrdinalIgnoreCase)));
			}
			UpdateSceneLabels();
		}
		RefreshStyles((SceneMode.IsChecked == true) ? _sceneSkin : cosmetic);
		ExportButton.IsEnabled = _operation == null && CanExport();
		if ((object)cosmetic != null)
		{
			UiMotion.Reveal((SceneMode.IsChecked == true) ? SceneSlots : AssetDetailsPanel, 0, 6.0);
			UiMotion.Reveal(StyleHint, 25, 4.0);
		}
	}

	private void SceneSelection_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_loaded)
		{
			_selectedSkins = SceneSkinsList.SelectedItems.OfType<Cosmetic>().ToArray();
			_selectedEmotes = SceneEmotesList.SelectedItems.OfType<Cosmetic>().ToArray();
			_sceneSkin = _selectedSkins.LastOrDefault();
			_sceneEmote = _selectedEmotes.LastOrDefault();
			UpdateSceneLabels();
			if (SceneMode.IsChecked == true)
			{
				RefreshStyles(_sceneSkin);
			}
			ExportButton.IsEnabled = _operation == null && CanExport();
		}
	}

	private void SetSceneListSelection(ListBox list, IEnumerable<Cosmetic> selected)
	{
		if (_syncingSceneSelections)
		{
			return;
		}
		_syncingSceneSelections = true;
		try
		{
			HashSet<Cosmetic> hashSet = selected.ToHashSet();
			list.SelectedItems.Clear();
			foreach (Cosmetic item in list.Items.OfType<Cosmetic>().Where(hashSet.Contains))
			{
				list.SelectedItems.Add(item);
			}
		}
		finally
		{
			_syncingSceneSelections = false;
		}
		if (list == SceneSkinsList)
		{
			_selectedSkins = list.SelectedItems.OfType<Cosmetic>().ToArray();
		}
		else
		{
			_selectedEmotes = list.SelectedItems.OfType<Cosmetic>().ToArray();
		}
		_sceneSkin = _selectedSkins.LastOrDefault();
		_sceneEmote = _selectedEmotes.LastOrDefault();
	}

	private bool CanExport()
	{
		if (SceneMode.IsChecked != true)
		{
			return CosmeticsGrid.SelectedItems.OfType<Cosmetic>().Any();
		}
		if (_selectedSkins.Count > 0)
		{
			return _selectedEmotes.Count > 0;
		}
		return false;
	}

	private void UpdateSceneLabels()
	{
		TextBlock sceneSkinLabel = SceneSkinLabel;
		string text;
		if (_selectedSkins.Count != 0)
		{
			text = ((_selectedSkins.Count == 1) ? _selectedSkins[0].Name : $"{_selectedSkins.Count:N0} skins selected");
		}
		else
		{
			text = "Nothing selected";
		}
		sceneSkinLabel.Text = text;
		TextBlock sceneEmoteLabel = SceneEmoteLabel;
		string text2;
		if (_selectedEmotes.Count != 0)
		{
			text2 = ((_selectedEmotes.Count == 1) ? _selectedEmotes[0].Name : $"{_selectedEmotes.Count:N0} emotes selected");
		}
		else
		{
			text2 = "Nothing selected";
		}
		sceneEmoteLabel.Text = text2;
		bool valueOrDefault = SceneMode.IsChecked == true;
		SceneSlots.Visibility = ((!valueOrDefault) ? Visibility.Collapsed : Visibility.Visible);
		AssetDetailsPanel.Visibility = (valueOrDefault ? Visibility.Collapsed : Visibility.Visible);
		SceneNamePanel.Visibility = ((!valueOrDefault) ? Visibility.Collapsed : Visibility.Visible);
		RefreshEmoteStyles();
		TextBlock modeHint = ModeHint;
		string text3;
		if (valueOrDefault)
		{
			if ((object)_sceneSkin == null)
			{
				text3 = "First, choose a skin. Then add an emote to animate it in Blender.";
			}
			else
			{
				text3 = (((object)_sceneEmote == null) ? "Skin ready. Choose an emote to complete the scene." : "Scene ready. Choose a style, name the scene and export.");
			}
		}
		else
		{
			text3 = "Select an asset from the library to prepare your export.";
		}
		modeHint.Text = text3;
	}

	private void Mode_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loaded)
		{
			return;
		}
		Cosmetic cosmetic = CosmeticsGrid.SelectedItem as Cosmetic;
		Cosmetic[] source = CosmeticsGrid.SelectedItems.OfType<Cosmetic>().ToArray();
		if (SceneMode.IsChecked == true)
		{
			if (source.Any((Cosmetic x) => x.Category.Equals("Skins", StringComparison.OrdinalIgnoreCase)))
			{
				SetSceneListSelection(SceneSkinsList, source.Where((Cosmetic x) => x.Category.Equals("Skins", StringComparison.OrdinalIgnoreCase)));
			}
			if (source.Any((Cosmetic x) => x.Category.Equals("Animations", StringComparison.OrdinalIgnoreCase)))
			{
				SetSceneListSelection(SceneEmotesList, source.Where((Cosmetic x) => x.Category.Equals("Animations", StringComparison.OrdinalIgnoreCase)));
			}
		}
		else if (cosmetic?.Category == "Skins")
		{
			_sceneSkin = cosmetic;
		}
		else if (cosmetic?.Category == "Animations")
		{
			_sceneEmote = cosmetic;
		}
		UpdateSceneLabels();
		RefreshStyles((SceneMode.IsChecked == true) ? _sceneSkin : cosmetic);
		ExportButton.Content = ((SceneMode.IsChecked == true) ? "Export scene →" : "Export asset →");
		ExportButton.IsEnabled = _operation == null && CanExport();
		UiMotion.Reveal(ModeHint, 0, 4.0);
		UiMotion.Reveal((SceneMode.IsChecked == true) ? SceneSlots : AssetDetailsPanel, 20);
		UiMotion.Reveal(SceneNamePanel, 40, 5.0);
	}

	private void RefreshStyles(Cosmetic? asset)
	{
        if (IsRocketLeague) { RefreshRocketStyles(asset); return; }
        if (!IsFortnite)
        {
            StyleChannelsTab.Items.Clear(); StyleChannelsTab.Visibility = Visibility.Collapsed;
            StyleSelector.Visibility = Visibility.Collapsed; StyleLabel.Text = "PC PART MODEL"; StyleCount.Text = "";
            StyleHint.Text = "Export the part’s enabled meshes together. Base-colour textures are included when available; custom shaders and runtime effects are simplified.";
            return;
        }
		StyleLabel.Text = ((SceneMode.IsChecked == true) ? "SKIN STYLE" : "STYLE");
        if (_scanning && _scanFortnite)
        {
            StyleChannelsTab.Visibility = Visibility.Collapsed;
            StyleSelector.Visibility = Visibility.Collapsed;
            StyleHint.Text = "Styles will be available when the Fortnite scan finishes.";
            return;
        }
		if ((object)asset != null && _styleAsset?.Id == asset.Id)
		{
			return;
		}
		_updatingStyles = true;
		try
		{
			_styleAsset = asset;
			StyleChannelsTab.Items.Clear();
			StyleChannelsTab.Visibility = Visibility.Collapsed;
			StyleSelector.Visibility = Visibility.Visible;
			if ((object)asset != null)
			{
				List<CosmeticStyleChannel> styleChannels = _session.GetStyleChannels(asset);
				if (styleChannels != null && styleChannels.Count > 0)
				{
					BuildStyleChannels(asset, styleChannels);
					return;
				}
			}
			string[] array = (((object)asset != null) ? _session.GetStyles(asset).ToArray() : new string[1] { "Default" });
			if (array.Length == 0)
			{
				array = new string[1] { "Default" };
			}
			List<StyleChoice> list = array.Select((string name) => new StyleChoice(name, asset?.ImageUrl)).ToList();
			StyleSelector.ItemsSource = list;
			string remembered = (((object)asset != null && _selectedStyles.TryGetValue(asset.Id, out string value)) ? value : "Default");
			StyleSelector.SelectedItem = list.FirstOrDefault((StyleChoice x) => x.Name.Equals(remembered, StringComparison.OrdinalIgnoreCase)) ?? list[0];
			if ((object)asset != null && StyleSelector.SelectedItem is StyleChoice styleChoice)
			{
				_selectedStyles[asset.Id] = styleChoice.Name;
			}
			StyleSelector.IsEnabled = (object)asset != null;
			StyleCount.Text = (((object)asset == null) ? "" : $"{list.Count} available");
			StyleLabel.Text = ((SceneMode.IsChecked == true) ? "SKIN STYLE" : "STYLE");
			TextBlock styleHint = StyleHint;
			string text;
			if ((object)asset == null)
			{
				text = "Choose an asset to load its available styles.";
			}
			else
			{
				text = ((asset.Category == "Animations" && array.Contains("Traversal")) ? "Traversal uses the moving animation. Static traversal uses its stationary version." : "Choose a style. Enable Save preview PNG below to include an image.");
			}
			styleHint.Text = text;
		}
		catch (Exception ex)
		{
			StyleSelector.ItemsSource = new StyleChoice[1]
			{
				new StyleChoice("Default", null)
			};
			StyleSelector.SelectedIndex = 0;
			StyleHint.Text = "Could not load styles. See the session log.";
			Report("Style lookup failed: " + ex.Message);
		}
		finally
		{
			_updatingStyles = false;
		}
	}

	private void Style_Changed(object sender, SelectionChangedEventArgs e)
	{
		if(IsRocketLeague) { if(!_updatingStyles && _styleAsset != null && StyleSelector.SelectedItem is RocketLeagueSession.Decal decal) _selectedStyles[_styleAsset.Id]=decal.Id; return; }
        if (!_updatingStyles && (object)_styleAsset != null && StyleSelector.SelectedItem is StyleChoice styleChoice)
		{
			_selectedStyles[_styleAsset.Id] = styleChoice.Name;
		}
	}

	private void BuildStyleChannels(Cosmetic asset, List<CosmeticStyleChannel> channels)
	{
		string saved = _selectedStyles.GetValueOrDefault(asset.Id, "Default");
		Dictionary<string, string> values = StyleSelection.Decode(saved);
		StyleSelector.Visibility = Visibility.Collapsed;
		StyleChannelsTab.Visibility = Visibility.Visible;
		StyleChannelsTab.Items.Clear();
		foreach (CosmeticStyleChannel channel in channels)
		{
			ComboBox combo = new ComboBox
			{
				ItemsSource = channel.Options,
				Margin = new Thickness(0.0, 8.0, 0.0, 6.0),
				MinWidth = 160.0
			};
			combo.ItemTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal'><Border Width='22' Height='22' CornerRadius='4' Background='{Binding Color}' Margin='0,0,8,0'/><TextBlock Text='{Binding Name}' VerticalAlignment='Center'/></StackPanel></DataTemplate>");
			combo.SelectedItem = channel.Options.FirstOrDefault((CosmeticStyleOption x) => x.Id == values.GetValueOrDefault(channel.Id)) ?? channel.Options.FirstOrDefault((CosmeticStyleOption x) => x.Name.Equals(saved, StringComparison.OrdinalIgnoreCase)) ?? channel.Options.First((CosmeticStyleOption x) => x.Id == channel.DefaultOption);
			values[channel.Id] = ((CosmeticStyleOption)combo.SelectedItem).Id;
			combo.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
			{
				if (combo.SelectedItem is CosmeticStyleOption cosmeticStyleOption)
				{
					values[channel.Id] = cosmeticStyleOption.Id;
					_selectedStyles[asset.Id] = StyleSelection.Encode(values);
				}
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(combo);
			stackPanel.Children.Add(new TextBlock
			{
				Text = $"{channel.Options.Count} options · combines with the other tabs",
				FontSize = 11.0,
				Foreground = (Brush)FindResource("Muted"),
				TextWrapping = TextWrapping.Wrap
			});
			StyleChannelsTab.Items.Add(new TabItem
			{
				Header = channel.Name,
				Content = stackPanel,
				Padding = new Thickness(8.0, 8.0, 8.0, 8.0),
				FontSize = 12.0
			});
		}
		StyleChannelsTab.SelectedIndex = 0;
		_selectedStyles[asset.Id] = StyleSelection.Encode(values);
		StyleCount.Text = $"{channels.Count} categories";
		StyleHint.Text = "Choose one option in each tab. All your selections are applied together.";
	}

	private void RefreshEmoteStyles()
	{
		if (!_loaded)
		{
			return;
		}
		_updatingEmoteStyles = true;
		try
		{
			List<string> list;
			if ((_scanning && _scanFortnite) || SceneMode.IsChecked != true || (object)_sceneEmote == null)
			{
				int num = 1;
				list = new List<string>(num);
				CollectionsMarshal.SetCount(list, num);
				CollectionsMarshal.AsSpan(list)[0] = "Default";
			}
			else
			{
				list = _session.GetStyles(_sceneEmote);
			}
			List<string> list2 = list;
			EmoteStylePanel.Visibility = ((!list2.Contains("Traversal")) ? Visibility.Collapsed : Visibility.Visible);
			EmoteStyleSelector.ItemsSource = list2;
			string text = (((object)_sceneEmote == null) ? "Default" : _selectedStyles.GetValueOrDefault(_sceneEmote.Id, "Traversal"));
			EmoteStyleSelector.SelectedItem = (list2.Contains(text) ? text : list2[0]);
			if ((object)_sceneEmote != null)
			{
				_selectedStyles[_sceneEmote.Id] = (string)EmoteStyleSelector.SelectedItem;
			}
		}
		catch (Exception ex)
		{
			EmoteStylePanel.Visibility = Visibility.Collapsed;
			Report("Could not load emote styles: " + ex.Message);
		}
		finally
		{
			_updatingEmoteStyles = false;
		}
	}

	private void EmoteStyle_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (!_updatingEmoteStyles && (object)_sceneEmote != null && EmoteStyleSelector.SelectedItem is string value)
		{
			_selectedStyles[_sceneEmote.Id] = value;
		}
	}

	private void CategoryTab_Click(object sender, RoutedEventArgs e)
	{
		ChooseCategory((string)((Button)sender).Tag);
	}

	private void ChooseSkin_Click(object sender, RoutedEventArgs e)
	{
		ChooseCategory("Skins");
	}

	private void ChooseEmote_Click(object sender, RoutedEventArgs e)
	{
		ChooseCategory("Animations");
	}

	private void ChooseCategory(string name)
	{
		Search.Text = "";
		Category.SelectedItem = Category.Items.OfType<ComboBoxItem>().First((ComboBoxItem x) => (string)x.Content == name);
		Cosmetic cosmetic;
		if (name == "Skins")
		{
			cosmetic = _sceneSkin;
		}
		else
		{
			cosmetic = ((name == "Animations") ? _sceneEmote : null);
		}
		if ((object)cosmetic != null)
		{
			CosmeticsGrid.SelectedItem = cosmetic;
			CosmeticsGrid.ScrollIntoView(cosmetic);
		}
		Search.Focus();
	}

	private void Detect_Click(object sender, RoutedEventArgs e)
	{
        if (!IsFortnite)
        {
            string? found = IsRocketLeague ? RocketLeagueSession.Detect() : UnitySession.DetectPcBuildingSimulator();
            if (found != null) GamePath.Text = found;
            Report(found != null ? (IsRocketLeague ? "Found Rocket League." : "Found PC Building Simulator 2.") : "Could not locate the game. Use Browse to choose its installation folder.");
            return;
        }
		(string, string)? tuple = Installation.Detect();
		if (tuple.HasValue)
		{
			(string, string) valueOrDefault = tuple.GetValueOrDefault();
			GamePath.Text = valueOrDefault.Item1;
			Report("Found " + valueOrDefault.Item2);
		}
		else
		{
			Report("Epic Launcher did not report a Fortnite installation. Use Browse to locate it.");
		}
	}

	private void BrowseGame_Click(object sender, RoutedEventArgs e)
	{
		ChooseFolder(GamePath);
	}

	private void BrowseExport_Click(object sender, RoutedEventArgs e)
	{
		ChooseFolder(ExportPath);
	}

	private async void RepairMaterials_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Choose an existing export to soften",
			Filter = "Blender scenes (*.blend)|*.blend",
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		string source = openFileDialog.FileName;
		await Run(async (AppSettings settings, IProgress<string> progress, CancellationToken ct) =>
		{
			string text = await Task.Run(() => BlenderMaterialRepair.CreateCopy(source, settings.ExportFolder, progress, ct), ct);
			_lastExport = Path.GetDirectoryName(text);
			Report("Saved a matte copy with softer highlights: " + Path.GetFileName(text));
			MessageBox.Show(this, "Open this corrected file in Blender:\n\n" + text + "\n\nThe original file is unchanged.", "Matte copy ready", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		});
	}

	private void ChooseFolder(TextBox target)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Choose folder"
		};
		if (Directory.Exists(target.Text))
		{
			openFolderDialog.InitialDirectory = target.Text;
		}
		if (openFolderDialog.ShowDialog(this) == true)
		{
			target.Text = openFolderDialog.FolderName;
		}
	}

	private void BrowseMapping_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Unreal mappings (*.usmap)|*.usmap"
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			MappingPath.Text = openFileDialog.FileName;
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		_operation?.Cancel();
		Report("Cancelling after the current archive operation…");
	}

	private void OpenFolder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string? obj = _lastExport ?? Path.GetFullPath(ExportPath.Text);
			Directory.CreateDirectory(obj);
			Process.Start(new ProcessStartInfo(obj)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Report(ex.Message);
		}
	}

	private void Log_Click(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo("notepad.exe")
		{
			ArgumentList = { _logFile },
			UseShellExecute = true
		});
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (_operation != null)
		{
			_operation.Cancel();
			e.Cancel = true;
			Report("Cancelling… close the window once the operation stops.");
		}
		else
		{
			ReadSettings().Save();
			_session.Dispose();
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e)
	{
		_lifetime.Cancel();
		_lifetime.Dispose();
		_updateHttp.Dispose();
		SystemParameters.StaticPropertyChanged -= SystemPreferences_Changed;
		base.OnClosed(e);
	}

}
