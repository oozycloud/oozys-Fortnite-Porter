using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FortniteObj.Core;

namespace FortniteObj;

public partial class MainWindow
{
    private bool _findingWebsiteId;

    private async void FindWebsiteId_Click(object sender, RoutedEventArgs e) => await FindWebsiteAsset(false);
    private async void ExportWebsiteId_Click(object sender, RoutedEventArgs e) => await FindWebsiteAsset(true);

    private async Task FindWebsiteAsset(bool export)
    {
        if (_operation != null || _findingWebsiteId) return;
        _findingWebsiteId = true;
        FindWebsiteIdButton.IsEnabled = ExportWebsiteIdButton.IsEnabled = false;
        try
        {
            if (!WebsiteAssetId.TryParse(WebsiteId.Text, out var requested))
            {
                Status.Text = "Paste a website ID such as G167 or G167S4. Unknown IDs may require an app update.";
                WebsiteId.Focus();
                return;
            }
            GameSelector.SelectedIndex = requested.Fortnite ? 0 : 1;
            if (_cosmetics.Count == 0)
            {
                await ScanLibrary(requested.Fortnite, (settings, progress, ct) => requested.Fortnite
                    ? _session.Scan(settings, progress, ct)
                    : _unitySession.Scan(settings.PcBuildingFolder, progress, ct));
                // A failed or cancelled scan already has a useful status message.
                if (!_gameCatalogs.TryGetValue(requested.Fortnite ? 0 : 1, out var scanned) || scanned.Count == 0) return;
                GameSelector.SelectedIndex = requested.Fortnite ? 0 : 1;
            }
            var matches = _cosmetics.Where(x => x.Id.Equals(requested.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                Status.Text = matches.Length == 0
                    ? "That ID is not in your scanned game files. Check the game folder, update the game, then rescan."
                    : "That ID is ambiguous in this installation. Nothing was exported.";
                return;
            }
            SingleMode.IsChecked = true;
            if (requested.StyleValues.Count > 0)
            {
                var channels = _session.GetStyleChannels(matches[0]);
                foreach (var choice in requested.StyleValues)
                {
                    var channel = channels.FirstOrDefault(c => c.Id == choice.Key);
                    var option = channel?.Options.FirstOrDefault(o => o.Id == choice.Value);
                    if (option == null || !option.Name.Equals(requested.StyleNames[choice.Key], StringComparison.Ordinal))
                        throw new InvalidOperationException("The game's style options changed. Update the app catalogue before exporting this style ID.");
                }
            }
            Category.SelectedIndex = 0;
            Search.Text = requested.ToString();
            _selectedStyles[matches[0].Id] = requested.Selection;
            _styleAsset = null;
            CosmeticsGrid.SelectedItems.Clear();
            CosmeticsGrid.SelectedItem = matches[0];
            RefreshStyles(matches[0]);
            CosmeticsGrid.ScrollIntoView(matches[0]);
            Status.Text = "Found " + matches[0].Name + ". Choose styles or export this asset.";
            if (export) Export_Click(sender: ExportWebsiteIdButton, e: new RoutedEventArgs());
        }
        catch (Exception ex)
        {
            Status.Text = "Could not find that asset: " + ex.Message;
        }
        finally
        {
            _findingWebsiteId = false;
            FindWebsiteIdButton.IsEnabled = ExportWebsiteIdButton.IsEnabled = _operation == null;
        }
    }
}
