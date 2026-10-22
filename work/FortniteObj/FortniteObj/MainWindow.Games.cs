using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FortniteObj.Core;

namespace FortniteObj;

public partial class MainWindow
{
    private readonly UnitySession _unitySession = new();
    private int _activeGame;
    private bool IsFortnite => _activeGame == 0;
    private bool IsRocketLeague => _activeGame == 2;
    private int _scanGame;
    private readonly RocketLeagueSession _rocketSession = new();
    private string _rocketLeagueFolder = "";
    private bool _scanFortnite;
    private readonly Dictionary<int, List<Cosmetic>> _gameCatalogs = new();
    private string _fortniteFolder = "", _pcBuildingFolder = "";

    private void Game_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (IsFortnite) _fortniteFolder = GamePath.Text;
        else if(IsRocketLeague) _rocketLeagueFolder = GamePath.Text;
        else _pcBuildingFolder = GamePath.Text;
        ApplyGameSelection();
        SaveAppearanceSettings();
    }

    private void ApplyGameSelection()
    {
        _gameCatalogs[_activeGame] = _cosmetics;
        _activeGame = Math.Max(0, GameSelector.SelectedIndex);
        if (_activeGame == 1 && string.IsNullOrWhiteSpace(_pcBuildingFolder))
            _pcBuildingFolder = UnitySession.DetectPcBuildingSimulator() ?? "";
        if(IsRocketLeague && string.IsNullOrWhiteSpace(_rocketLeagueFolder)) _rocketLeagueFolder = RocketLeagueSession.Detect() ?? "";
        GamePath.Text = IsFortnite ? _fortniteFolder : IsRocketLeague ? _rocketLeagueFolder : _pcBuildingFolder;
        GameFolderTitle.Text = IsFortnite ? "Fortnite folder" : IsRocketLeague ? "Rocket League" : "PC Building Simulator 2";
        GameDescription.Text = IsFortnite ? "Choose the game folder, then scan to load skins, emotes, skyboxes and prefabs." : IsRocketLeague ? "Browse cars, compatible decals, wheels, goal explosions, maps, balls and other installed assets." : "Browse GPUs, motherboards, CPUs, coolers, fans and other PC parts.";
        GameSupportTitle.Text = IsFortnite ? "STREAMED FILES" : IsRocketLeague ? "ROCKET LEAGUE ASSETS" : "PC PART MODELS";
        GameSupportHint.Text = IsFortnite ? "Missing parts and larger textures are recovered during export when available." : IsRocketLeague ? "Car decals are filtered by known compatibility. Maps export mesh libraries; Unreal particles and gameplay are supplied as resources where supported." : "Each entry is a named PC part. Its enabled meshes stay together, with their original placement and available base-colour textures.";
        ScanButton.Content = IsFortnite ? "Scan Fortnite files" : IsRocketLeague ? "Scan Rocket League files" : "Scan PC Building Simulator 2";
        SceneMode.Visibility = IsFortnite ? Visibility.Visible : Visibility.Collapsed;
        SingleMode.IsChecked = true;
        SavePng.Visibility = IsFortnite ? Visibility.Visible : Visibility.Collapsed;
        AutoCompatibility.IsEnabled = IsFortnite;
        EngineVersion.IsEnabled = MappingPath.IsEnabled = MainKey.IsEnabled = IsFortnite;
        CosmeticsGrid.Columns[0].Visibility = IsFortnite ? Visibility.Visible : Visibility.Collapsed;
        Category.SelectedIndex = 0;
        string[] categories = IsFortnite ? new[] { "Sidekicks", "Emoticons", "Prefabs" } : IsRocketLeague ? new[] { "Cars", "Goal explosions", "Balls" } : new[] { "GPUs", "Motherboards", "CPUs" };
        Button[] buttons = { GameCategory0, GameCategory1, GameCategory2 };
        for (int i = 0; i < buttons.Length; i++) buttons[i].Content = buttons[i].Tag = categories[i];
        if (_operation == null) { Activity.IsIndeterminate = false; Activity.Value = 0; ProgressText.Text = "Ready"; }
        InspectorScroll.ScrollToTop();
        foreach (ComboBoxItem item in Category.Items)
        {
            string name = item.Content?.ToString() ?? "";
            bool unity = UnitySession.PartCategories.Contains(name);
            bool rocket = RocketLeagueSession.Categories.Contains(name);
            item.Visibility = name == "All assets" || (IsFortnite ? !unity && !rocket : IsRocketLeague ? rocket : unity) ? Visibility.Visible : Visibility.Collapsed;
        }
        _cosmetics = _gameCatalogs.GetValueOrDefault(_activeGame) ?? new List<Cosmetic>();
        _sceneSkin = _sceneEmote = _styleAsset = null;
        _selectedSkins = _selectedEmotes = Array.Empty<Cosmetic>();
        _selectedStyles.Clear();
        Search.Text = "";
        UpdateSceneLabels();
        RefreshFilter();
        RefreshStyles(null);
        ExportButton.IsEnabled = false;
        OutputFormatHint.Text = IsFortnite ? ExchangeExport.Hint(ExchangeExport.Formats[Math.Max(0, ExportFormat.SelectedIndex)]) : IsRocketLeague ? "Meshes and available textures export in the selected format. Resource-only effects retain their original extracted files." : "Export a PC part model in the selected format. Blender keeps its mesh hierarchy and available base-colour textures.";
        if (_operation == null) Status.Text = IsFortnite ? "Ready. Choose your Fortnite folder to get started." : IsRocketLeague ? "Ready. Scan Rocket League to browse installed assets." : "Ready. Scan PC Building Simulator 2 to browse PC part models.";
    }

    private void RefreshRocketStyles(Cosmetic? asset)
    {
        if(asset != null && _styleAsset?.Id == asset.Id) {RocketAppearancePanel.Visibility=Visibility.Visible;return;}
        _updatingStyles=true;
        try {
            _styleAsset=asset; StyleChannelsTab.Items.Clear(); StyleChannelsTab.Visibility=Visibility.Collapsed;
            StyleLabel.Text=asset?.Category=="Cars"?"DECAL STYLE":"STYLE";
            var choices=new List<RocketLeagueSession.Decal>{new("Default","No decal","")};
            if(asset?.Category=="Cars") choices.AddRange(_rocketSession.GetDecals(asset));
            StyleSelector.Visibility=Visibility.Visible; StyleSelector.ItemsSource=choices;
            if(asset!=null && !_rocketAppearances.ContainsKey(asset.Id))_rocketAppearances[asset.Id]=new();
            string remembered=asset==null?"Default":_selectedStyles.GetValueOrDefault(asset.Id,_rocketAppearances[asset.Id].DecalId);
            StyleSelector.SelectedItem=choices.FirstOrDefault(d=>d.Id==remembered)??choices[0];
            if(asset!=null){string selected=((RocketLeagueSession.Decal)StyleSelector.SelectedItem).Id;_selectedStyles[asset.Id]=selected;_rocketAppearances[asset.Id].DecalId=selected;}
            StyleSelector.IsEnabled=asset?.Category=="Cars";
            StyleCount.Text=asset?.Category=="Cars"?$"{choices.Count-1} known compatible decals":"";
            BuildRocketAppearance(asset);
            StyleHint.Text=asset?.Category=="Cars"?"Choose a decal compatible with this car. Paint and animated shader effects are simplified; original extracted textures and parameters are retained.":"Maps export a mesh library. Goal explosions and other runtime effects export their available resources; Unreal simulation is not reproduced.";
        } finally {_updatingStyles=false;}
    }

    private async Task RefreshCompatibilityAtLaunch()
    {
        _ = RefreshRocketCompatibilityAtLaunch();
        CompatibilityStatus.Text = "Checking current Fortnite keys and mappings…";
        try
        {
            var result = await Compatibility.PublicCache.Refresh(new Progress<string>(message => CompatibilityStatus.Text = message), _lifetime.Token, force: true);
            CompatibilityStatus.Text = (result.Cached ? "Using saved compatibility • " : "Up to date • ") + result.Version + "\nChecked " + DateTime.Now.ToString("HH:mm") + ". Custom overrides are kept.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            CompatibilityStatus.Text = "Fortnite refresh unavailable. Scan again when connected, or use matching custom files.";
            File.AppendAllText(_logFile, "Compatibility refresh: " + ex.Message + "\n");
        }
    }

    private async Task RefreshRocketCompatibilityAtLaunch()
    {
        try { await _rocketSession.RefreshCompatibility(_lifetime.Token); }
        catch(OperationCanceledException) { }
        catch(Exception ex) { File.AppendAllText(_logFile,"Rocket League compatibility: "+ex.Message+"\n"); }
    }

    private async Task ExportUnitySelection()
    {
        var assets = CosmeticsGrid.SelectedItems.OfType<Cosmetic>().ToArray();
        bool rocket = IsRocketLeague;
        var styles = assets.ToDictionary(a=>a.Id,a=>_selectedStyles.GetValueOrDefault(a.Id,"Default"));
        if (assets.Length == 0) return;
        await Run(async (settings, progress, ct) =>
        {
            var results = new List<ExportResult>();
            foreach (Cosmetic asset in assets)
            {
                ct.ThrowIfCancellationRequested();
                var result = rocket ? await _rocketSession.Export(asset, styles[asset.Id], settings, progress, ct, _rocketAppearances.GetValueOrDefault(asset.Id)) : await _unitySession.Export(asset, settings, progress, ct);
                results.Add(result);
                _lastExport = result.Folder;
                foreach (string warning in result.Warnings) File.AppendAllText(_logFile, "WARNING: " + warning + "\n");
            }
            Report($"Exported {results.Count} {(rocket ? "Rocket League" : "PC Building Simulator 2")} asset(s).");
            string detail = rocket ? string.Join("\n", results.SelectMany(r=>r.Warnings).Distinct()) : "PC part models retain their enabled mesh hierarchy and available base-colour textures. Custom Unity shaders and runtime effects are simplified. Review export warnings for any missing materials.";
            MessageBox.Show(this, $"Saved {results.Count} export(s). Last folder:\n{_lastExport}\n\n{detail}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}
