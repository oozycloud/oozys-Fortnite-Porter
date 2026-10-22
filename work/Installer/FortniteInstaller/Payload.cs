using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PorterRelease;

namespace FortniteInstaller;

internal static class Payload
{
	private const string Marker = "app/installation.json";

	internal static string SafePath(string root, string relative)
	{
		if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
		{
			throw new IOException("Invalid installation file path.");
		}
		string[] array = relative.Split('\\', '/');
		foreach (string text in array)
		{
			bool flag = (((text != null && text.Length == 0) || text == "." || text == "..") ? true : false);
			if (flag || text.EndsWith('.') || text.EndsWith(' ') || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(text, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|$)", RegexOptions.IgnoreCase))
			{
				throw new IOException("Invalid installation file name.");
			}
		}
		string fullPath = Path.GetFullPath(Path.Combine(root, relative));
		if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new IOException("A file path leaves the installation folder.");
		}
		string text2 = fullPath;
		while (text2.Length > root.Length)
		{
			if ((File.Exists(text2) || Directory.Exists(text2)) && (File.GetAttributes(text2) & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("The installation contains a linked folder or file. Choose a normal application folder.");
			}
			text2 = Path.GetDirectoryName(text2);
		}
		return fullPath;
	}

	internal static string Root(string folder)
	{
		if (string.IsNullOrWhiteSpace(folder))
		{
			throw new IOException("Choose an installation folder.");
		}
		string text = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Trim().Trim('"')));
		if (text == Path.GetPathRoot(text))
		{
			throw new IOException("Choose an application folder, not a drive root.");
		}
		if (File.Exists(text))
		{
			throw new IOException("The installation location is a file. Choose a folder.");
		}
		return text;
	}

	internal static InstalledProduct? Detect(string folder)
	{
		string text = Root(folder);
		string path = SafePath(text, "app/installation.json");
		if (File.Exists(path))
		{
			InstalledProduct installedProduct = JsonSerializer.Deserialize<InstalledProduct>(File.ReadAllText(path));
			if (installedProduct?.ProductId != "oozycloud.fortnite-porter" || installedProduct.Files == null || installedProduct.Launcher == null || !Regex.IsMatch(installedProduct.Version ?? "", "^(beta )?\\d+\\.\\d+\\.\\d+$") || !Regex.IsMatch(installedProduct.Launcher, "^oozys-Fortnite-Porter \\((beta )?\\d+\\.\\d+\\.\\d+\\)\\.exe$"))
			{
				throw new IOException("The existing installation record is invalid.");
			}
			string[] files = installedProduct.Files;
			foreach (string text2 in files)
			{
				SafePath(text, text2);
				if (!text2.Replace('\\', '/').StartsWith("app/", StringComparison.OrdinalIgnoreCase) && text2 != installedProduct.Launcher)
				{
					throw new IOException("The installation record contains an unrelated file.");
				}
			}
			return installedProduct;
		}
		if (!Directory.Exists(text))
		{
			return null;
		}
		foreach (string item in Directory.EnumerateFiles(text, "Fortnite Porter Studio v*.exe"))
		{
			Match match = Regex.Match(Path.GetFileName(item), "^Fortnite Porter Studio v(\\d+\\.\\d+\\.\\d+)\\.exe$");
			if (!match.Success || !File.Exists(Path.ChangeExtension(item, "runtimeconfig.json")))
			{
				continue;
			}
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteInstaller.legacy-files.json");
			string[] source = ((stream == null) ? Array.Empty<string>() : (JsonSerializer.Deserialize<string[]>(stream) ?? Array.Empty<string>()));
			source = source.Select((string x) => x.Replace("v2.1.0", "v" + match.Groups[1].Value)).ToArray();
			return new InstalledProduct("oozycloud.fortnite-porter", match.Groups[1].Value, Path.GetFileName(item), source.Concat(new _003C_003Ez__ReadOnlySingleElementList<string>("installed-files.json")).ToArray());
		}
		return null;
	}

	internal static void Extract(string folder, IProgress<string>? progress = null)
	{
		using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteInstaller.payload.zip") ?? throw new IOException("The installer payload is missing.");
		ExtractStream(folder, payload, progress);
	}

	internal static void ExtractStream(string folder, Stream payload, IProgress<string>? progress = null, Action<int>? commitProbe = null)
	{
		string text = Root(folder);
		InstalledProduct installedProduct = Detect(text);
		if ((object)installedProduct != null && ProductInfo.IsNewerInstallation(installedProduct.Version))
		{
			throw new IOException("A newer version is installed here. Choose another folder to avoid downgrading it.");
		}
		Directory.CreateDirectory(text);
		using (new FileStream(SafePath(text, ".porter-install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose))
		{
			string text2 = SafePath(text, ".porter-stage-" + Guid.NewGuid().ToString("N"));
			string text3 = SafePath(text, ".porter-backup-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(text2);
			Directory.CreateDirectory(text3);
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			bool flag = false;
			try
			{
				using ZipArchive zipArchive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
				ZipArchiveEntry[] array = zipArchive.Entries.Where((ZipArchiveEntry x) => !string.IsNullOrEmpty(x.Name)).ToArray();
				HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				ZipArchiveEntry[] array2 = array;
				foreach (ZipArchiveEntry obj in array2)
				{
					string text4 = obj.FullName.Replace('\\', '/');
					if (text4 != "oozys-Fortnite-Porter (beta 0.6.0).exe" && !text4.StartsWith("app/", StringComparison.Ordinal))
					{
						throw new IOException("The installer contains an unexpected file.");
					}
					if (!names.Add(text4))
					{
						throw new IOException("The installer contains duplicate file names.");
					}
					SafePath(text, text4);
					string text5 = SafePath(text2, text4);
					Directory.CreateDirectory(Path.GetDirectoryName(text5));
					obj.ExtractToFile(text5);
					if (names.Count % 20 == 0)
					{
						progress?.Report($"Preparing files… {names.Count} / {array.Length}");
					}
				}
				if (new string[6] { "oozys-Fortnite-Porter (beta 0.6.0).exe", "app/Porter.exe", "app/Porter.dll", "app/Porter.runtimeconfig.json", "app/hostfxr.dll", "app/coreclr.dll" }.Any((string name) => !names.Contains(name)))
				{
					throw new IOException("The installer is incomplete.");
				}
				names.Add("app/installation.json");
				File.WriteAllText(SafePath(text2, "app/installation.json"), JsonSerializer.Serialize(new InstalledProduct("oozycloud.fortnite-porter", "beta 0.6.0", "oozys-Fortnite-Porter (beta 0.6.0).exe", names.Order().ToArray())));
				HashSet<string> hashSet = (installedProduct?.Files ?? Array.Empty<string>()).Select((string x) => x.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
				string[] array3 = names.Concat(hashSet).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
				string[] array4 = array3;
				foreach (string text6 in array4)
				{
					string path = SafePath(text, text6);
					if (Directory.Exists(path))
					{
						throw new IOException("An installation file conflicts with a folder: " + text6);
					}
					if (!File.Exists(path))
					{
						continue;
					}
					if (!hashSet.Contains(text6))
					{
						throw new IOException("This folder contains a conflicting file that does not belong to the installation: " + text6 + ". Choose another application folder.");
					}
					try
					{
						using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
						{
						}
					}
					catch (IOException)
					{
						throw new IOException("Close oozy's Fortnite Porter and any files open from its app folder, then try again.");
					}
				}
				array4 = array3;
				foreach (string text7 in array4)
				{
					string text8 = SafePath(text, text7);
					if (File.Exists(text8))
					{
						string text9 = SafePath(text3, text7);
						Directory.CreateDirectory(Path.GetDirectoryName(text9));
						File.Move(text8, text9);
						list.Add(text7);
					}
				}
				progress?.Report(((object)installedProduct == null) ? "Installing…" : "Updating…");
				foreach (string item in names)
				{
					string text10 = SafePath(text, item);
					Directory.CreateDirectory(Path.GetDirectoryName(text10));
					File.Move(SafePath(text2, item), text10);
					list2.Add(item);
					commitProbe?.Invoke(list2.Count);
				}
				foreach (string item2 in from x in (from x in hashSet
						select Path.GetDirectoryName(x) into x
						where !string.IsNullOrEmpty(x)
						select x).Distinct()
					orderby x.Length descending
					select x)
				{
					try
					{
						string path2 = SafePath(text, item2);
						if (Directory.Exists(path2) && !Directory.EnumerateFileSystemEntries(path2).Any())
						{
							Directory.Delete(path2);
						}
					}
					catch (IOException)
					{
					}
				}
				progress?.Report("Installed successfully.");
			}
			catch
			{
				try
				{
					foreach (string item3 in list2.AsEnumerable().Reverse())
					{
						File.Delete(SafePath(text, item3));
					}
					foreach (string item4 in list.AsEnumerable().Reverse())
					{
						File.Move(SafePath(text3, item4), SafePath(text, item4), overwrite: true);
					}
				}
				catch
				{
					flag = true;
					throw new IOException("The update could not be restored automatically. Your previous files are preserved in " + text3);
				}
				throw;
			}
			finally
			{
				string[] array4 = ((!flag) ? new string[2] { text2, text3 } : new string[1] { text2 });
				foreach (string text11 in array4)
				{
					try
					{
						if (text11.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
						{
							Directory.Delete(text11, recursive: true);
						}
					}
					catch (IOException)
					{
					}
				}
			}
		}
	}
}
