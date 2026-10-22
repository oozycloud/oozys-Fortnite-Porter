using System;
using System.IO;
using System.Text.Json;

namespace FortniteObj.Core;

public sealed class AppSettings
{
	public string SelectedGame { get; set; } = "Fortnite";

    public System.Collections.Generic.Dictionary<string,RocketAppearance> RocketAppearances {get;set;} = new();

    public string RocketLeagueFolder { get; set; } = "";

    public string PcBuildingFolder { get; set; } = "";

	public string GameFolder { get; set; } = "";

	public string ExportFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Fortnite OBJ Exports");

	public string EngineVersion { get; set; } = "GAME_UE5_8";

	public string MappingFile { get; set; } = "";

	public string MainKey { get; set; } = "";

	public bool AutoCompatibility { get; set; } = true;

	public bool DownloadHighResolution { get; set; } = true;

	public bool OnlyBlend { get; set; }

	public string ExportFormat { get; set; } = "BLEND";

	public bool ExportAnimations { get; set; } = true;

    public bool TriangulateMeshes { get; set; }

    public int TextureSizeLimit { get; set; }

	public bool MatteMaterials { get; set; } = true;

	public string AnimatedTheme { get; set; } = "Embers";

	public bool ReduceMotion { get; set; }

	public bool CheckForUpdatesAutomatically { get; set; } = true;

	public double AnimationFps { get; set; }

	public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteObj");

	public static AppSettings Load()
	{
		try
		{
			return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(DataFolder, "settings.json"))) ?? new AppSettings();
		}
		catch
		{
			return new AppSettings();
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(DataFolder);
		File.WriteAllText(Path.Combine(DataFolder, "settings.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}
}
