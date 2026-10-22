using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.IO.Objects.OnDemand;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace FortniteObj.Core;

public sealed class QualityFileProvider : DefaultFileProvider
{
	private readonly string _paks;

	private readonly Lazy<IoChunkToc[]> _tocs;

	private readonly Dictionary<string, IoStoreOnDemandReader> _readers = new Dictionary<string, IoStoreOnDemandReader>();

	private readonly Dictionary<string, string> _indexFailures = new Dictionary<string, string>();

	private readonly Dictionary<FIoChunkId, byte[]?> _chunks = new Dictionary<FIoChunkId, byte[]>();

	private readonly Dictionary<FPackageId, (IoChunkToc Toc, IOnDemandContainerEntry Container)?> _packageLocations = new Dictionary<FPackageId, (IoChunkToc, IOnDemandContainerEntry)?>();

	private readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30L)
	};

	private readonly IoStoreOnDemandDownloader _downloader;

	public bool EnablePackageStreaming { get; set; }

	public IProgress<string>? Progress { get; set; }

	public bool DownloadMissing { get; set; } = true;

	public CancellationToken ExportCancellation { get; set; }

	public void BeginExport(CancellationToken ct)
	{
		ExportCancellation = ct;
		_indexFailures.Clear();
	}

	public QualityFileProvider(string paks, VersionContainer versions)
		: base(paks, SearchOption.TopDirectoryOnly, versions, StringComparer.OrdinalIgnoreCase)
	{
		_paks = paks;
		DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(AppSettings.DataFolder, "texture-chunks"));
		directoryInfo.Create();
		_downloader = new IoStoreOnDemandDownloader(new IoStoreOnDemandOptions
		{
			ChunkHostUri = new Uri("https://egdownload.fastly-edge.com/"),
			ChunkCacheDirectory = directoryInfo,
			DownloaderClient = _http
		});
		_tocs = new Lazy<IoChunkToc[]>(() => (from path in Directory.EnumerateFiles(_paks, "*.uondemandtoc")
			select new IoChunkToc(path, Versions)).ToArray());
	}

	public override IPackage LoadPackage(GameFile file)
	{
		FIoStoreEntry entry = file as FIoStoreEntry;
		if (entry == null)
		{
			return base.LoadPackage(file);
		}
		EnsureOptionalSegment(entry);
		FIoContainerHeader containerHeader = entry.IoStoreReader.ContainerHeader;
		if (EnablePackageStreaming && DownloadMissing && containerHeader != null)
		{
			int num = Array.IndexOf(containerHeader.PackageIds, entry.ChunkId.AsPackageId());
			if (num >= 0)
			{
				FPackageId[] importedPackages = containerHeader.StoreEntries[num].ImportedPackages;
				foreach (FPackageId id in importedPackages)
				{
					EnsurePackage(id);
				}
			}
		}
		Files.FindPayloads(file, out GameFile _, out IReadOnlyList<GameFile> bulk, out IReadOnlyList<GameFile> optional);
		return new IoPackage(file.CreateReader(), containerHeader, (FByteBulkDataHeader? header) => ((bulk.Count > 0) ? bulk[0].SafeCreateReader(header) : null) ?? Payload(entry, EIoChunkType5.BulkData), (FByteBulkDataHeader? header) => ((optional.Count > 0) ? optional[0].SafeCreateReader(header) : null) ?? Payload(entry, EIoChunkType5.OptionalBulkData), this, FindOptionalSegment(entry));
	}

	private IoOptionalSegment? FindOptionalSegment(FIoStoreEntry entry)
	{
		if (entry.IsOptionalSegmentPackage || !entry.IsPackageData) return null;
		if (!Files.TryGetOptionalSegmentPackage(entry.ChunkId.AsPackageId(), out var file)) return null;
		if (file is not FIoStoreEntry optionalEntry || ReferenceEquals(optionalEntry, entry) || !optionalEntry.IsUePackage) return null;
		ExportCancellation.ThrowIfCancellationRequested();
		Files.FindPayloads(optionalEntry, out _, out var bulk, out var optional);
		return new IoOptionalSegment(optionalEntry.CreateReader(),
			header => (bulk.Count > 0 ? bulk[0].SafeCreateReader(header) : null) ?? Payload(optionalEntry, EIoChunkType5.BulkData),
			header => (optional.Count > 0 ? optional[0].SafeCreateReader(header) : null) ?? Payload(optionalEntry, EIoChunkType5.OptionalBulkData),
			optionalEntry.IoStoreReader.ContainerHeader);
	}

	public string? StreamContainer(string packagePath)
	{
		FPackageId id = FPackageId.FromName(packagePath.Split('.')[0]);
		return LocatePackage(id)?.Container.ContainerName;
	}

	private (IoChunkToc Toc, IOnDemandContainerEntry Container)? LocatePackage(FPackageId id)
	{
		if (_packageLocations.TryGetValue(id, out (IoChunkToc, IOnDemandContainerEntry)? value))
		{
			return value;
		}
		FIoChunkId chunkId = new FIoChunkId(id.id, 0, EIoChunkType5.ExportBundleData);
		IoChunkToc[] value2 = _tocs.Value;
		foreach (IoChunkToc ioChunkToc in value2)
		{
			foreach (IOnDemandContainerEntry container in ioChunkToc.OnDemandToc.Containers)
			{
				if (container.TryGetFileEntryHash(chunkId, out var _))
				{
					return _packageLocations[id] = (ioChunkToc, container);
				}
			}
		}
		return _packageLocations[id] = null;
	}

	public void EnsurePackage(string objectPath)
	{
		if (!TryGetGameFile(objectPath.Split('.')[0], out GameFile _))
		{
			EnsurePackage(FPackageId.FromName(objectPath.Split('.')[0]));
		}
	}

	private void EnsurePackage(FPackageId id)
	{
		if (!EnablePackageStreaming || !DownloadMissing || FilesById.ContainsKey(id))
		{
			return;
		}
		ExportCancellation.ThrowIfCancellationRequested();
		(IoChunkToc, IOnDemandContainerEntry)? tuple = LocatePackage(id);
		if (!tuple.HasValue)
		{
			return;
		}
		(IoChunkToc, IOnDemandContainerEntry) valueOrDefault = tuple.GetValueOrDefault();
		IoStoreOnDemandReader ioStoreOnDemandReader = StreamReader(valueOrDefault.Item1, valueOrDefault.Item2);
		MountStreamReader(ioStoreOnDemandReader, valueOrDefault.Item2.ContainerName);
	}

	private void EnsureOptionalSegment(FIoStoreEntry entry)
	{
		if (!EnablePackageStreaming || !DownloadMissing || entry.IsOptionalSegmentPackage || Files.TryGetOptionalSegmentPackage(entry.ChunkId.AsPackageId(), out _)) return;
		ExportCancellation.ThrowIfCancellationRequested();
		var chunk = new FIoChunkId(entry.ChunkId.ChunkId, 1, EIoChunkType5.ExportBundleData);
		foreach (var toc in _tocs.Value)
		foreach (var container in toc.OnDemandToc.Containers)
		{
			if (!container.TryGetFileEntryHash(chunk, out _)) continue;
			var reader = StreamReader(toc, container);
			MountStreamReader(reader, container.ContainerName);
			if (Files.TryGetOptionalSegmentPackage(entry.ChunkId.AsPackageId(), out _)) return;
		}
	}

	private void MountStreamReader(IoStoreOnDemandReader ioStoreOnDemandReader, string containerName)
	{
		if (!MountedVfs.Contains(ioStoreOnDemandReader))
		{
			Progress?.Report("Opening streamed cosmetic parts in " + containerName + "…");
			PostLoadReader(ioStoreOnDemandReader);
			FAesKey aesKey = ioStoreOnDemandReader.AesKey;
			if (aesKey != null)
			{
				SubmitKey(ioStoreOnDemandReader.EncryptionKeyGuid, aesKey);
			}
			else
			{
				Mount();
			}
			if (!MountedVfs.Contains(ioStoreOnDemandReader))
			{
				throw new IOException("Could not open streamed cosmetic archive " + containerName + ". Its AES key may be unavailable.");
			}
		}
	}

	private IoStoreOnDemandReader StreamReader(IoChunkToc toc, IOnDemandContainerEntry container)
	{
		string text = container.UTocHash.ToString();
		if (_readers.TryGetValue(text, out IoStoreOnDemandReader value))
		{
			return value;
		}
		if (_indexFailures.TryGetValue(text, out string value2))
		{
			throw new IOException(value2);
		}
		Progress?.Report("Loading the stream index for " + container.ContainerName + "…");
		string relative = toc.OnDemandToc.ChunksDirectory + "/" + text.ToLowerInvariant() + ".utoc";
		IoStoreOnDemandReader parsed = null;
		try
		{
			ArchiveIndexDownload.Get(relative, text, container.ContainerName, Progress, (byte[] data) =>
			{
				parsed = new IoStoreOnDemandReader(new FByteArchive(container.ContainerName + "_OnDemand.utoc", data, Versions), toc, container, _downloader);
			}, ExportCancellation).GetAwaiter().GetResult();
		}
		catch (Exception ex) when (!ExportCancellation.IsCancellationRequested)
		{
			string text2 = "Could not load archive index " + container.ContainerName + ": " + ex.Message;
			_indexFailures[text] = text2;
			Progress?.Report(text2);
			throw new IOException(text2, ex);
		}
		value = parsed;
		if (Keys.TryGetValue(value.EncryptionKeyGuid, out FAesKey value3))
		{
			value.AesKey = value3;
		}
		_readers.Add(text, value);
		return value;
	}

	private FArchive? Payload(FIoStoreEntry owner, EIoChunkType5 type)
	{
		ExportCancellation.ThrowIfCancellationRequested();
		FIoChunkId fIoChunkId = new FIoChunkId(owner.ChunkId.ChunkId, 0, type);
		if (_chunks.TryGetValue(fIoChunkId, out byte[] value))
		{
			if (value != null)
			{
				return new FByteArchive(owner.Path + "." + type, value, Versions);
			}
			return null;
		}
		foreach (IoStoreReader item in MountedVfs.OfType<IoStoreReader>())
		{
			if (item.DoesChunkExist(fIoChunkId))
			{
				byte[] array = item.Read(fIoChunkId);
				_chunks[fIoChunkId] = array;
				return new FByteArchive(owner.Path + "." + type, array, Versions);
			}
		}
		if (!DownloadMissing)
		{
			return null;
		}
		IoChunkToc[] value2 = _tocs.Value;
		foreach (IoChunkToc ioChunkToc in value2)
		{
			foreach (IOnDemandContainerEntry container in ioChunkToc.OnDemandToc.Containers)
			{
				if (container.TryGetFileEntryHash(fIoChunkId, out var _))
				{
					ExportCancellation.ThrowIfCancellationRequested();
					IoStoreOnDemandReader ioStoreOnDemandReader = StreamReader(ioChunkToc, container);
					Progress?.Report("Reading high-resolution data for " + Path.GetFileNameWithoutExtension(owner.Path) + "…");
					byte[] array2 = ioStoreOnDemandReader.Read(fIoChunkId);
					_chunks[fIoChunkId] = array2;
					return new FByteArchive(owner.Path + "." + type, array2, Versions);
				}
			}
		}
		_chunks[fIoChunkId] = null;
		return null;
	}

	public override void Dispose()
	{
		IoStoreOnDemandReader[] array = _readers.Values.Where((IoStoreOnDemandReader r) => !MountedVfs.Contains(r) && !UnloadedVfs.Contains(r)).ToArray();
		base.Dispose();
		IoStoreOnDemandReader[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			array2[num].Dispose();
		}
		_readers.Clear();
		_chunks.Clear();
		_packageLocations.Clear();
		_downloader.Dispose();
		_http.Dispose();
	}
}
