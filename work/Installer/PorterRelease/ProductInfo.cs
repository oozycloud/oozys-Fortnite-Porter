using System;

namespace PorterRelease;

public static class ProductInfo
{
	public const string Name = "oozy's Fortnite Porter";

	public const string Version = "beta 0.7.0";

	public const string NumericVersion = "0.7.0";

	public const string ReleaseTag = "beta-0.7.0";

	public const string Id = "oozycloud.fortnite-porter";

	public const string Launcher = "oozys-Fortnite-Porter (beta 0.7.0).exe";

	public const string Repository = "https://github.com/oozycloud/oozys-Fortnite-Porter";

	public const string Releases = "https://github.com/oozycloud/oozys-Fortnite-Porter/releases";

	public static bool IsNewerInstallation(string version)
	{
		if (version.StartsWith("beta ", StringComparison.OrdinalIgnoreCase))
		{
			if (System.Version.TryParse(version.Substring(5), out Version result))
			{
				return result > System.Version.Parse("0.7.0");
			}
			return false;
		}
		if (System.Version.TryParse(version, out Version result2))
		{
			return result2 > new Version(2, 2, 0);
		}
		return false;
	}
}
