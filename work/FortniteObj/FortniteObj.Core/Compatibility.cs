using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse_Conversion.Textures.BC;

namespace FortniteObj.Core;

public static class Compatibility
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(45L)
	};

	public static readonly CompatibilityCache PublicCache = new(Http, AppSettings.DataFolder);

	public static async Task<CompatibilityData> Prepare(AppSettings settings, IProgress<string> progress, CancellationToken ct)
	{
		string cache = AppSettings.DataFolder;
		Directory.CreateDirectory(cache);
		progress.Report("Preparing archive decompression…");
		await OodleHelper.InitializeAsync(Path.Combine(cache, OodleHelper.OodleFileName), ct);
		if (OodleHelper.Instance == null)
		{
			throw new IOException("Oodle could not be loaded. Check your internet connection and try again.");
		}
		await ZlibHelper.InitializeAsync(Path.Combine(cache, ZlibHelper.DllName), ct);
		string detexPath = Path.Combine(cache, "Detex.dll");
		if (!(await DetexHelper.LoadDllAsync(detexPath)))
		{
			throw new IOException("The texture decoder could not be initialized.");
		}
		DetexHelper.Initialize(detexPath);
		Dictionary<FGuid, FAesKey> keys = new Dictionary<FGuid, FAesKey>();
		string mapping = settings.MappingFile;
		string version = "Custom compatibility files";
		if (settings.AutoCompatibility)
		{
            var snapshot = await PublicCache.Refresh(progress, ct);
            JsonNode aes = snapshot.Aes;
            JsonNode jsonNode = snapshot.Mappings;
			version = aes["version"]?.GetValue<string>() ?? "";
			keys[default] = new FAesKey(aes["mainKey"].GetValue<string>());
			if (aes["dynamicKeys"] is JsonArray source)
			{
				foreach (JsonObject item in source.OfType<JsonObject>())
				{
					keys[new FGuid(item["guid"].GetValue<string>())] = new FAesKey(item["key"].GetValue<string>());
				}
			}
            if (string.IsNullOrWhiteSpace(mapping)) mapping = snapshot.MappingPath;
		}
		if (!string.IsNullOrWhiteSpace(settings.MainKey))
		{
			keys[default] = new FAesKey(settings.MainKey.Trim());
		}
		if (keys.Count == 0)
		{
			throw new IOException("Enter the AES key for this game build, or enable automatic compatibility.");
		}
		if (!File.Exists(mapping))
		{
			throw new FileNotFoundException("Select the .usmap file for this Fortnite build, or enable automatic compatibility.", mapping);
		}
		return new CompatibilityData(mapping, keys, version);
	}

}
