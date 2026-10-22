using System;
using System.IO;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;

namespace FortniteObj.Core;

// v5 adds FEngineVersion to the optional header; the mapping payload remains v4.
// Format reference: FabianFG/CUE4Parse, EUsmapVersion and UsmapParser.
// Normalize only the in-memory envelope after parsing that metadata. Never edit
// the downloaded mapping or reinterpret an unknown future format as v4.
public sealed class CurrentUsmapProvider : UsmapTypeMappingsProvider
{
    private readonly string? path;
    private readonly byte[]? source;
    public FEngineVersion? EngineVersion { get; private set; }

    public CurrentUsmapProvider(string path)
    {
        this.path = path;
        Load(path);
    }

    public CurrentUsmapProvider(byte[] bytes)
    {
        source = bytes;
        Load(bytes);
    }

    public override void Reload()
    {
        if (path != null) Load(path);
        else Load(source!);
    }
    public override void Load(string path, StringComparer? comparer = null) => Load(File.ReadAllBytes(path), comparer);

    public override void Load(byte[] bytes, StringComparer? comparer = null)
    {
        EngineVersion = null;
        try
        {
            if (bytes.Length < 3 || bytes[0] != 0xC4 || bytes[1] != 0x30)
                throw new IOException("The mapping file is empty, truncated or has an invalid signature.");
            if (bytes[2] > 5)
                throw new IOException($"Mapping format {bytes[2]} needs a newer porter build. Check for updates.");
            if (bytes[2] == 5)
            {
                using var archive = new FByteArchive("Fortnite mapping header", bytes);
                archive.Position = 3;
                bool hasVersioning = archive.ReadBoolean();
                int metadataStart = checked((int)archive.Position);
                if (hasVersioning) EngineVersion = new FEngineVersion(archive);
                int metadataEnd = checked((int)archive.Position);
                var normalized = new byte[bytes.Length - (metadataEnd - metadataStart)];
                Array.Copy(bytes, 0, normalized, 0, metadataStart);
                Array.Copy(bytes, metadataEnd, normalized, metadataStart, bytes.Length - metadataEnd);
                normalized[2] = 4;
                base.Load(normalized, comparer);
            }
            else base.Load(bytes, comparer);
        }
        catch (Exception ex) when (ex is not IOException and not OutOfMemoryException)
        {
            throw new IOException("The Fortnite mapping file could not be read. Refresh compatibility or select a valid mapping for your game build.", ex);
        }
    }
}
