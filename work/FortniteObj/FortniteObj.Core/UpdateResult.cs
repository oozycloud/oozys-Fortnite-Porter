namespace FortniteObj.Core;

public record UpdateResult(bool Available, string Message, string? ReleaseUrl = null, string? Version = null);
