namespace FortniteObj.Core;

public record Cosmetic(string Id, string Name, string Category, string ObjectPath, string Availability = "Check on export", string? PackagePath = null, string? ImageUrl = null, string? FortniteGgId = null);
