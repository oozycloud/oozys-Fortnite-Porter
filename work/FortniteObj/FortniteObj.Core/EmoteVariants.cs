using System;

namespace FortniteObj.Core;

public static class EmoteVariants
{
	public const string Traversal = "Traversal";

	public const string Static = "Static traversal";

	public static bool IsMotion(string name, string? slot)
	{
		if ((slot == null || !slot.Contains("InMotion", StringComparison.OrdinalIgnoreCase)) && !name.Contains("traverse", StringComparison.OrdinalIgnoreCase))
		{
			return name.Contains("traversal", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool IsStatic(string name)
	{
		if (!name.Contains("static", StringComparison.OrdinalIgnoreCase))
		{
			return name.Contains("stationary", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool Include(string name, string? slot, string choice, bool hasMotion, bool hasStatic)
	{
		if (choice == "Static traversal")
		{
			if (!IsMotion(name, slot))
			{
				if (hasStatic)
				{
					return IsStatic(name);
				}
				return true;
			}
			return false;
		}
		if (!hasMotion)
		{
			return !IsStatic(name);
		}
		return IsMotion(name, slot);
	}
}
