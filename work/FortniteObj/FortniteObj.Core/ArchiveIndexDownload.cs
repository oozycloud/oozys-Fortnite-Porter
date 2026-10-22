using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteObj.Core;

public static class ArchiveIndexDownload
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = Timeout.InfiniteTimeSpan
	};

	public static async Task<byte[]> Get(string relative, string hash, string label, IProgress<string>? progress, Action<byte[]> validate, CancellationToken ct)
	{
		string text = Path.Combine(AppSettings.DataFolder, "texture-chunks");
		Directory.CreateDirectory(text);
		string target = Path.Combine(text, hash.ToLowerInvariant() + ".utoc");
		if (File.Exists(target))
		{
			byte[] array = await File.ReadAllBytesAsync(target, ct).ConfigureAwait(false);
			try
			{
				validate(array);
				return array;
			}
			catch (Exception) when (!ct.IsCancellationRequested)
			{
			}
		}
		string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
		try
		{
			using CancellationTokenSource headers = CancellationTokenSource.CreateLinkedTokenSource(ct);
			headers.CancelAfter(TimeSpan.FromSeconds(30L));
			using HttpResponseMessage response = await Http.GetAsync(new Uri(new Uri("https://egdownload.fastly-edge.com/"), relative), HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			long? total = response.Content.Headers.ContentLength;
			using (Stream input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
			{
				using FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, useAsync: true);
				byte[] buffer = new byte[131072];
				long received = 0L;
				DateTime last = DateTime.MinValue;
				while (true)
				{
					using CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
					idle.CancelAfter(TimeSpan.FromSeconds(45L));
					int count = await input.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
					if (count == 0)
					{
						break;
					}
					await output.WriteAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
					received += count;
					if ((DateTime.UtcNow - last).TotalMilliseconds >= 250.0 || received == total)
					{
						progress?.Report((total > 0) ? $"Downloading archive index {received}/{total} bytes · {label}" : $"Downloading archive index · {received:N0} bytes · {label}");
						last = DateTime.UtcNow;
					}
					continue;
				}
				if (total.HasValue && received != total)
				{
					throw new IOException("The archive index download was incomplete.");
				}
			}
			byte[] array2 = await File.ReadAllBytesAsync(temporary, ct).ConfigureAwait(false);
			validate(array2);
			File.Move(temporary, target, overwrite: true);
			return array2;
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}
	}
}
