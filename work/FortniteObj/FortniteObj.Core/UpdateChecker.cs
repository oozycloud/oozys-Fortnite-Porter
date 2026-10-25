using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public sealed class UpdateChecker
{
	private readonly HttpClient _client;

	public UpdateChecker(HttpClient client)
	{
		_client = client;
	}

	public static Version? ParseVersion(string? tag)
	{
		Match match = Regex.Match(tag ?? "", "^(?:v|beta[ -])?(\\d+)\\.(\\d+)\\.(\\d+)(?:\\+[0-9A-Za-z.-]+)?$", RegexOptions.IgnoreCase);
		if (match.Success)
		{
			InlineArray3<string> buffer = default;
			buffer[0] = match.Groups[1].Value;
			buffer[1] = match.Groups[2].Value;
			buffer[2] = match.Groups[3].Value;
			if (Version.TryParse(string.Join('.', (ReadOnlySpan<string?>)buffer), out Version result))
			{
				return result;
			}
		}
		return null;
	}

	public async Task<UpdateResult> Check(CancellationToken ct = default(CancellationToken))
	{
		_ = 1;
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/oozycloud/oozys-Fortnite-Porter/releases?per_page=100");
			request.Headers.UserAgent.ParseAdd("oozys-Fortnite-Porter/beta-0.7.0");
			request.Headers.Accept.ParseAdd("application/vnd.github+json");
			request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
			timeout.CancelAfter(TimeSpan.FromSeconds(15L));
			using HttpResponseMessage response = await _client.SendAsync(request, timeout.Token);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return new UpdateResult(Available: false, "No public release is available on GitHub yet.");
			}
			HttpStatusCode statusCode = response.StatusCode;
			if ((statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.TooManyRequests) ? true : false)
			{
				return new UpdateResult(Available: false, "GitHub limited update checks. Try again later.");
			}
			if (!response.IsSuccessStatusCode)
			{
				return new UpdateResult(Available: false, "GitHub could not check for updates. Try again later.");
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
			{
				return new UpdateResult(Available: false, "GitHub returned an unreadable release list. Try again later.");
			}
			List<(JsonElement, Version, string)> list = new List<(JsonElement, Version, string)>();
			foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
			{
				if (item.TryGetProperty("draft", out var value) && value.GetBoolean())
				{
					continue;
				}
				string text = item.GetProperty("tag_name").GetString();
				Version version = ParseVersion(text);
				if ((object)version != null)
				{
					bool flag = text.StartsWith("beta", StringComparison.OrdinalIgnoreCase);
					if ((!item.TryGetProperty("prerelease", out var value2) || !value2.GetBoolean() || flag) && (flag || !(version >= new Version(1, 0, 0)) || !(version <= new Version(2, 2, 0))))
					{
						list.Add((item, version, (flag ? "beta " : "") + version));
					}
				}
			}
			if (list.Count == 0)
			{
				return new UpdateResult(Available: false, "No beta release is available on GitHub yet.");
			}
			(JsonElement, Version, string) tuple = list.OrderByDescending(((JsonElement Release, Version Version, string Label) x) => x.Version).First();
			var (jsonElement, _, _) = tuple;
			if (tuple.Item2 <= ParseVersion("beta 0.7.0"))
			{
				return new UpdateResult(Available: false, "You're up to date — version beta 0.7.0.");
			}
			if (!Uri.TryCreate(jsonElement.GetProperty("html_url").GetString(), UriKind.Absolute, out Uri result) || result.Scheme != "https" || result.Host != "github.com" || !result.IsDefaultPort || !string.IsNullOrEmpty(result.UserInfo) || !result.AbsolutePath.StartsWith("/oozycloud/oozys-Fortnite-Porter/releases/tag/", StringComparison.OrdinalIgnoreCase))
			{
				return new UpdateResult(Available: false, "GitHub returned an invalid release link.");
			}
			return new UpdateResult(Available: true, "Version " + tuple.Item3 + " is available.", result.AbsoluteUri, tuple.Item3);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			return new UpdateResult(Available: false, "Update check timed out. Try again later.");
		}
		catch (HttpRequestException)
		{
			return new UpdateResult(Available: false, "Couldn't reach GitHub. Check your connection and try again.");
		}
		catch (Exception ex3) when ((ex3 is JsonException || ex3 is InvalidOperationException || ex3 is KeyNotFoundException || ex3 is FormatException) ? true : false)
		{
			return new UpdateResult(Available: false, "GitHub returned an unreadable release. Try again later.");
		}
	}
}
