using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Math;

namespace FortniteObj.Core;

public sealed partial class GameSession
{
    private Dictionary<string, RigSceneExporter.Attachment> ResolvePartAttachments(UObject definition,
        string packagePath, string style, List<UObject> meshes, List<string> warnings)
    {
        var result = new Dictionary<string, RigSceneExporter.Attachment>(StringComparer.OrdinalIgnoreCase);
        var parts = definition.Properties.Where(p => p.Name.Text is "BaseCharacterParts" or "CharacterParts")
            .SelectMany(p => ReferencedObjects(p.Tag?.GenericValue, warnings)).ToList();
        if (!string.Equals(style, "Default", StringComparison.OrdinalIgnoreCase))
            parts.AddRange(FindStyleOptions(packagePath, style).SelectMany(option => option.Properties
                .Where(p => p.Name.Text is "VariantParts" or "OuterParts" or "VariantOuterParts" or "AdditionalParts")
                .SelectMany(p => ReferencedObjects(p.Tag?.GenericValue, warnings))));
        foreach (var part in parts.Distinct())
        {
            var data = part.Properties.Where(p => p.Name.Text == "AdditionalData")
                .SelectMany(p => ReferencedObjects(p.Tag?.GenericValue, warnings));
            string socketName = data.Select(x => TextValue(x.Properties.FirstOrDefault(p => p.Name.Text == "AttachSocketName")?.Tag?.GenericValue))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x != "None");
            if (socketName == null) continue;
            foreach (var accessory in PartMeshes(part, warnings).Where(mesh => meshes.Any(x => x.GetPathName().Equals(mesh.GetPathName(), StringComparison.OrdinalIgnoreCase))))
            {
                // Full character skeletons already contain their authored placement and weights.
                if (accessory is USkeletalMesh skinned && skinned.ReferenceSkeleton.FinalRefBoneInfo.Length > 1) continue;
                bool found = false;
                foreach (var body in meshes.OfType<USkeletalMesh>().OrderByDescending(x => x.ReferenceSkeleton.FinalRefBoneInfo.Length))
                {
                    var sockets = body.Sockets.Concat(body.Skeleton?.Load<USkeleton>()?.Sockets ?? []);
                    var socket = sockets.Select(x => x.Load<USkeletalMeshSocket>())
                        .FirstOrDefault(x => x != null && x.SocketName.Text.Equals(socketName, StringComparison.OrdinalIgnoreCase));
                    if (socket == null) continue;
                    var skeleton = body.ReferenceSkeleton;
                    int boneIndex = Array.FindIndex(skeleton.FinalRefBoneInfo, x => x.Name.Text.Equals(socket.BoneName.Text, StringComparison.OrdinalIgnoreCase));
                    if (boneIndex < 0) continue;
                    var world = Matrix4x4.Identity;
                    for (int i = boneIndex; i >= 0; i = skeleton.FinalRefBoneInfo[i].ParentIndex)
                    {
                        var pose = skeleton.FinalRefBonePose[i];
                        world *= Matrix(pose.Translation, pose.Rotation, pose.Scale3D);
                    }
                    world = Matrix(socket.RelativeLocation, socket.RelativeRotation.Quaternion(), socket.RelativeScale) * world;
                    if (!Matrix4x4.Decompose(world, out var scale, out var rotation, out var translation))
                        throw new InvalidOperationException("The attachment socket transform could not be decomposed: " + socketName);
                    var transform = new RigSceneExporter.SceneTransform([translation.X, translation.Y, translation.Z], [0,0,0],
                        [scale.X, scale.Y, scale.Z], [rotation.X, rotation.Y, rotation.Z, rotation.W]);
                    result[accessory.GetPathName()] = new RigSceneExporter.Attachment(RigSceneExporter.BonesFrom(body), boneIndex, transform);
                    found = true;
                    break;
                }
                if (!found) warnings.Add(part.Name + ": attachment socket '" + socketName + "' could not be resolved.");
            }
        }
        return result;

        static Matrix4x4 Matrix(FVector location, FQuat rotation, FVector scale) =>
            Matrix4x4.CreateScale(scale.X, scale.Y, scale.Z) *
            Matrix4x4.CreateFromQuaternion(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)) *
            Matrix4x4.CreateTranslation(location.X, location.Y, location.Z);
    }
}
