using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FortniteObj.Core;

public static class StyleSelection
{
	public const string Prefix = "channels:";

	public static string Encode(Dictionary<string, string> values)
	{
		return "channels:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values)));
	}

	public static Dictionary<string, string> Decode(string value)
	{
		if (!value.StartsWith("channels:", StringComparison.Ordinal))
		{
			return new Dictionary<string, string>();
		}
		return JsonSerializer.Deserialize<Dictionary<string, string>>(Convert.FromBase64String(value.Substring("channels:".Length))) ?? new Dictionary<string, string>();
	}
}
