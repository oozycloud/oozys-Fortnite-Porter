using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public sealed class FortniteGgCatalog
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(20L)
	};

	private readonly Dictionary<string, string> _ids;

	private FortniteGgCatalog(Dictionary<string, string> ids)
	{
		_ids = ids;
	}

	public static async Task<FortniteGgCatalog> LoadAsync(IProgress<string> progress, CancellationToken ct)
	{
		string cache = Path.Combine(AppSettings.DataFolder, "fortnite-gg-items.json");
		string json = null;
		try
		{
			progress.Report("Loading Fortnite.GG preview images…");
			json = await Http.GetStringAsync("https://fortnite.gg/api/items.json", ct);
			Directory.CreateDirectory(AppSettings.DataFolder);
			File.WriteAllText(cache, json);
		}
		catch when (!ct.IsCancellationRequested)
		{
			try
			{
				json = File.ReadAllText(cache);
			}
			catch
			{
			}
			progress.Report((json == null) ? "Fortnite.GG images unavailable; continuing with local asset names." : "Using cached Fortnite.GG preview images.");
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (!string.IsNullOrWhiteSpace(json))
		{
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(json);
				foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
				{
					if (item.Value.ValueKind == JsonValueKind.Number && item.Value.TryGetInt32(out var value))
					{
						dictionary[item.Name] = value.ToString(CultureInfo.InvariantCulture);
					}
					else if (item.Value.ValueKind == JsonValueKind.String && int.TryParse(item.Value.GetString(), out value))
					{
						dictionary[item.Name] = value.ToString(CultureInfo.InvariantCulture);
					}
				}
			}
			catch
			{
			}
		}
		return new FortniteGgCatalog(dictionary);
	}

	public Cosmetic Enrich(Cosmetic cosmetic)
	{
		if (!_ids.TryGetValue(cosmetic.Id, out string value))
		{
			string key = cosmetic.Id.Split('.').FirstOrDefault() ?? cosmetic.Id;
			_ids.TryGetValue(key, out value);
		}
		bool flag;
		switch (cosmetic.Category)
		{
		case "Skins":
		case "Pickaxes":
		case "Back blings":
		case "Gliders":
		case "Animations":
		case "Emoticons":
		case "Sprays":
		case "Loading screens":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return cosmetic with
			{
				FortniteGgId = value,
				ImageUrl = "https://fortnite-api.com/images/cosmetics/br/" + Uri.EscapeDataString(cosmetic.Id.ToLowerInvariant()) + "/icon.png"
			};
		}
		return cosmetic with
		{
			FortniteGgId = value
		};
	}
}
