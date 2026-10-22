using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FortniteObj.Core;

public static class Installation
{
	public static (string Path, string Version)? Detect()
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic\\EpicGamesLauncher\\Data\\Manifests");
		if (!Directory.Exists(path))
		{
			return null;
		}
		foreach (string item in Directory.EnumerateFiles(path, "*.item"))
		{
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(item));
				JsonElement rootElement = jsonDocument.RootElement;
				if (rootElement.TryGetProperty("DisplayName", out var value) && value.GetString() == "Fortnite")
				{
					return (rootElement.GetProperty("InstallLocation").GetString(), rootElement.GetProperty("AppVersionString").GetString() ?? "");
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public static string Paks(string folder)
	{
		string fullPath = Path.GetFullPath(folder.Trim().Trim('"'));
		string text = Path.Combine(fullPath, "FortniteGame", "Content", "Paks");
		if (Directory.Exists(text))
		{
			return text;
		}
		if (Directory.Exists(fullPath) && (Directory.EnumerateFiles(fullPath, "*.utoc").Any() || Directory.EnumerateFiles(fullPath, "*.pak").Any()))
		{
			return fullPath;
		}
		throw new DirectoryNotFoundException("Choose your Fortnite installation or its FortniteGame\\Content\\Paks folder.");
	}

	public static string SafeName(string name)
	{
		string text = Regex.Replace(name, "[^a-zA-Z0-9_.-]+", "_").Trim(new char[2] { '.', '_' });
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Substring(0, Math.Min(100, text.Length));
		}
		return "asset";
	}
}
