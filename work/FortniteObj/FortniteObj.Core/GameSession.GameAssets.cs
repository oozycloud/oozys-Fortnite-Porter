using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace FortniteObj.Core;

public sealed partial class GameSession
{
    // Actual player building blueprints include the authored edited geometry.
    // Environment walls and the editor's pattern metadata are not build pieces.
    internal static bool IsBuildPackage(string path) =>
        path.Contains("/Building/ActorBlueprints/Player/", StringComparison.OrdinalIgnoreCase)
        && Path.GetFileNameWithoutExtension(path).StartsWith("PBWA_", StringComparison.OrdinalIgnoreCase);

    private static string AssetDisplayName(UObject asset, string category, string? displayName)
    {
        if (category == "Builds")
        {
            var parts = asset.Name.Replace("_C", "").Split('_', 3);
            if (parts.Length < 3) return asset.Name;
            string material = parts[1][0] switch { 'W' => "Wood", 'S' => "Brick", 'M' => "Metal", _ => parts[1] };
            string piece = parts[2] switch
            {
                "Solid" => "Wall", "DoorC" => "Wall · centre door", "DoorS" or "DoorSide" => "Wall · side door",
                "WindowC" or "WindowsC" => "Wall · centre window", "WindowSide" or "WindowsSide" => "Wall · side window",
                "Windows" => "Wall · windows", "HalfWall" or "HalfWallS" => "Wall · half height",
                "HalfWallHalf" => "Wall · half corner", "HalfWallDoor" => "Wall · half height door",
                "HalfWallDoorS" or "HalfWallDoorSide" => "Wall · half height side door",
                "QuarterWallS" => "Wall · low", "QuarterWallHalf" => "Wall · low corner",
                "Archway" => "Wall · arch", "ArchwayLarge" => "Wall · large arch",
                "ArchwayLargeSupport" => "Wall · arch support", "StairW" => "Stairs · full width",
                "StairF" => "Stairs · straight edit", "StairR" => "Stairs · right edit",
                "StairT" => "Stairs · turning edit", "StairSpiral" => "Stairs · spiral",
                "RoofC" => "Cone", "RoofS" => "Cone · side edit", "RoofD" => "Cone · diagonal edit",
                "RoofI" => "Cone · inner edit", "RoofO" => "Cone · outer edit", "RoofWall" => "Cone · wall edit",
                "BalconyS" => "Floor · side edit", "BalconyD" => "Floor · diagonal edit",
                "BalconyI" => "Floor · inner edit", "BalconyO" => "Floor · outer edit",
                _ => Regex.Replace(parts[2], "([a-z])([A-Z])", "$1 $2")
            };
            return $"{material} {piece} · level {parts[1][1..]}";
        }
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            if (category == "Weapons")
            {
                string rarity = Regex.Match(asset.Name, @"_(UC|SR|VR|UR|R|C)(?:_|$)").Groups[1].Value;
                rarity = rarity switch { "C" => "Common", "UC" => "Uncommon", "R" => "Rare", "VR" => "Epic", "SR" => "Legendary", "UR" => "Mythic", _ => "" };
                return displayName.Trim() + (rarity.Length > 0 ? " · " + rarity : "");
            }
            return displayName.Trim();
        }
        if (category == "Vehicles")
        {
            string name = Regex.Replace(asset.Name, @"^VID_(?:Valet_)?", "");
            name = name.Replace("SportsCar", "Whiplash").Replace("BasicCar", "Prevalent").Replace("BasicTruck", "Bear")
                .Replace("BigRig", "Mudflap").Replace("GolfCart", "ATK").Replace("ShoppingCart", "Shopping Cart")
                .Replace("Hoagie", "Choppa").Replace("Meatball", "Motorboat").Replace("Octopus", "Baller");
            return Regex.Replace(name.Replace("VehicleSK", "").Replace("Vehicle", "").Replace('_', ' '), @"\s+", " ").Trim();
        }
        return asset.Name;
    }

    private ExportResult ExportGameAsset(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend, bool matteMaterials)
    {
        var definition = _provider!.LoadPackageObject(cosmetic.ObjectPath);
        var warnings = new List<string>();
        var missing = new List<string>();
        var instances = new List<MapMeshInstance>();
        if (cosmetic.Category == "Weapons")
        {
            // The definition's override is the selected rarity model. Walking the
            // actor as well would add its default model on top of the selected one.
            foreach (string field in new[] { "WeaponMeshOverride", "PickupSkeletalMesh", "PickupStaticMesh" })
            {
                var meshes = NamedValues(definition.Properties, field).SelectMany(v => ReferencedObjects(v, missing))
                    .Where(x => x is USkeletalMesh or UStaticMesh).DistinctBy(x => x.GetPathName()).ToList();
                if (meshes.Count == 0) continue;
                instances.AddRange(meshes.Select(m => new MapMeshInstance(m, SceneTransform(FTransform.Identity))));
                break;
            }
        }
        if (instances.Count == 0)
        {
            var classes = cosmetic.Category == "Builds" ? new[] { definition }.OfType<UClass>() :
                NamedValues(definition.Properties, cosmetic.Category == "Vehicles" ? "VehicleActorClass" : "WeaponActorClass")
                    .SelectMany(v => ReferencedObjects(v, missing)).OfType<UClass>();
            foreach (var actorClass in classes) instances.AddRange(GameActorInstances(actorClass, warnings, missing, ct));
        }
        if (missing.Count > 0) throw new IOException("Asset references could not be loaded: " + string.Join("; ", missing.Distinct()));
        if (instances.Count == 0) throw new IOException("No renderable geometry was found for " + cosmetic.Name + ". No empty export was created.");
        return ExportInstances(cosmetic, root, instances, warnings, progress, ct, onlyBlend, matteMaterials);
    }

    private List<MapMeshInstance> GameActorInstances(UClass actorClass, List<string> warnings, List<string> missing, CancellationToken ct)
    {
        var result = new List<MapMeshInstance>();
        var componentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>();
        // Derived components win; inherited components fill the missing pieces.
        for (UClass? type = actorClass; type != null && visited.Add(type.GetPathName()) && visited.Count <= 24; type = type.SuperStruct?.Load() as UClass)
        {
            ct.ThrowIfCancellationRequested();
            if (!type.GetPathName().StartsWith("/", StringComparison.Ordinal) || type.GetPathName().StartsWith("/Script/", StringComparison.Ordinal)) break;
            _provider!.EnsurePackage(type.GetPathName());
            var cdo = type.ClassDefaultObject?.Load();
            var exports = _provider.LoadPackage(type.GetPathName().Split('.')[0]).GetExports().ToArray();
            foreach (var component in exports.Where(x => x.ExportType.Contains("MeshComponent", StringComparison.Ordinal)))
            {
                if (component.GetOrDefault("bIsEditorOnly", false) || component.GetOrDefault("bHiddenInGame", false) || !component.GetOrDefault("bVisible", true)) continue;
                var meshes = new[] { "SkeletalMesh", "SkinnedAsset", "StaticMesh" }.SelectMany(f => NamedValues(component.Properties, f))
                    .SelectMany(v => ReferencedObjects(v, missing)).Where(x => x is USkeletalMesh or UStaticMesh).DistinctBy(x => x.GetPathName()).ToArray();
                if (meshes.Length == 0 || !componentNames.Add(component.Name)) continue;
                var transform = component is USceneComponent scene ? ComponentWorld(scene) : FTransform.Identity;
                var materials = component.GetOrDefault<FPackageIndex[]>("OverrideMaterials");
                result.AddRange(meshes.Select(m => new MapMeshInstance(m, SceneTransform(transform), Materials: materials)));
            }
            if (cdo != null && result.Count == 0 && ActorMesh(cdo) is UStaticMesh mesh)
                result.Add(new MapMeshInstance(mesh, SceneTransform(FTransform.Identity), Materials: ActorMaterials(cdo)));
            if (cdo?.GetOrDefault<FPackageIndex>("DoorMesh")?.Load<UStaticMesh>() is UStaticMesh door && componentNames.Add("__door"))
            {
                var offset = cdo.GetOrDefault("DoorOffset", FVector.ZeroVector);
                result.Add(new MapMeshInstance(door, SceneTransform(new FTransform(FQuat.Identity, offset, FVector.OneVector))));
            }
        }
        return result;
    }
}
