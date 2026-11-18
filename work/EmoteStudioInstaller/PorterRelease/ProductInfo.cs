using System;

namespace PorterRelease;

public static class ProductInfo
{
	public const string Name = "oozy's Emote Studio";

	public const string Version = "beta 0.0.1";

	public const string NumericVersion = "0.0.1";

	public const string ReleaseTag = "emote-studio-beta-0.0.1";

	public const string Id = "oozycloud.emote-studio";

	public const string Launcher = "oozys-Emote-Studio (beta 0.0.1).exe";

	public const string Repository = "https://github.com/oozycloud/oozys-Fortnite-Porter";

	public const string Releases = "https://github.com/oozycloud/oozys-Fortnite-Porter/releases";

	public static bool IsNewerInstallation(string version)
	{
		if (version.StartsWith("beta ", StringComparison.OrdinalIgnoreCase))
		{
			if (System.Version.TryParse(version.Substring(5), out Version result))
			{
				return result > System.Version.Parse("0.0.1");
			}
			return false;
		}
		if (System.Version.TryParse(version, out Version result2))
		{
			return result2 > new Version(0, 0, 1);
		}
		return false;
	}
}
