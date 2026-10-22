using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.PhysicsEngine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse_Conversion.Dto;

namespace FortniteObj.Core;

public sealed class RigSceneExporter
{
	public record Bone(string Name, int Parent, float[] Translation, float[] Rotation, float[] Scale, string TranslationMode = "Skeleton");

	public record Influence(int Bone, float Weight);

	public record Part(string Name, float[][] Positions, float[][] Normals, float[][] UVs, uint[][] Faces, int[] FaceMaterials, string[] Materials, Bone[] Bones, Influence[][] Weights, bool RigidRoot, SceneTransform? Transform = null, string[]? PhysicsBones = null, float[][][]? ExtraUVs = null, float[][]? VertexColors = null, string? OverlayMaterial = null);

	public record SceneTransform(float[] Location, float[] Rotation, float[] Scale, float[]? Quaternion = null);

	public List<Part> Parts { get; } = new List<Part>();

    public record Attachment(Bone[] Bones, int BoneIndex, SceneTransform Transform);

    internal static Bone[] BonesFrom(USkeletalMesh mesh)
    {
			FReferenceSkeleton skeleton = mesh.ReferenceSkeleton;
			return skeleton.FinalRefBoneInfo.Select((FMeshBoneInfo bone, int i) =>
			{
				FTransform fTransform = skeleton.FinalRefBonePose[i];
				return new Bone(bone.Name.Text, bone.ParentIndex, new float[3]
				{
					fTransform.Translation.X,
					fTransform.Translation.Y,
					fTransform.Translation.Z
				}, new float[4]
				{
					fTransform.Rotation.X,
					fTransform.Rotation.Y,
					fTransform.Rotation.Z,
					fTransform.Rotation.W
				}, new float[3]
				{
					fTransform.Scale3D.X,
					fTransform.Scale3D.Y,
					fTransform.Scale3D.Z
				});
			}).ToArray();
    }

	public void Add<T>(UObject source, MeshDto<T> dto, MeshLodDto<T> lod, int number, List<string> warnings, SceneTransform? transform = null, string? overlayMaterial = null, Attachment? attachment = null) where T : struct, IMeshVertex
	{
        Bone[] bones = attachment?.Bones ?? (source is USkeletalMesh mesh ? BonesFrom(mesh) : Array.Empty<Bone>());
		bool flag = bones.Length == 0;
		if (flag && (object)transform == null)
		{
			bones = new Bone[1]
			{
				new Bone(Installation.SafeName(source.Name) + "_root", -1, new float[3], new float[4] { 0f, 0f, 0f, 1f }, new float[3] { 1f, 1f, 1f })
			};
		}
		Influence[][] array = new Influence[lod.Vertices.Length][];
		for (int num = 0; num < array.Length; num++)
		{
            if (attachment != null)
            {
                array[num] = [new Influence(attachment.BoneIndex, 1f)];
                continue;
            }
			if (flag)
			{
				array[num] = (((object)transform != null) ? Array.Empty<Influence>() : new Influence[1]
				{
					new Influence(0, 1f)
				});
				continue;
			}
			T val = lod.Vertices[num];
			Influence[] source2 = (from w in (val is SkinnedMeshVertex skinnedMeshVertex) ? skinnedMeshVertex.Influences : Array.Empty<MeshBoneInfluenceDto>()
				where w.Weight > 0f && float.IsFinite(w.Weight) && w.Bone < bones.Length
				group w by w.Bone into g
				select new Influence(g.Key, g.Sum((MeshBoneInfluenceDto w) => w.Weight))).ToArray();
			float total = source2.Sum((Influence w) => w.Weight);
			if (total <= 0f)
			{
				throw new IOException($"{source.Name}: vertex {num} has no usable bone weights; cannot build a faithful rig.");
			}
			array[num] = source2.Select((Influence w) => w with
			{
				Weight = w.Weight / total
			}).ToArray();
		}
		List<uint[]> list = new List<uint[]>();
		List<int> list2 = new List<int>();
		MeshSectionDto[] sections = lod.Sections;
		for (int num2 = 0; num2 < sections.Length; num2++)
		{
			MeshSectionDto meshSectionDto = sections[num2];
			for (int num3 = meshSectionDto.FirstIndex; num3 < meshSectionDto.FirstIndex + meshSectionDto.NumFaces * 3; num3 += 3)
			{
				list.Add(new uint[3]
				{
					lod.Indices[num3],
					lod.Indices[num3 + 1],
					lod.Indices[num3 + 2]
				});
				list2.Add((meshSectionDto.MaterialIndex >= 0 && meshSectionDto.MaterialIndex < dto.Materials.Length) ? meshSectionDto.MaterialIndex : dto.Materials.Length);
			}
		}
		string[] array2 = Array.Empty<string>();
		if (source is USkeletalMesh uSkeletalMesh2)
		{
			try
			{
				UPhysicsAsset uPhysicsAsset = uSkeletalMesh2.PhysicsAsset?.Load<UPhysicsAsset>();
				if (uPhysicsAsset != null)
				{
					array2 = (from x in uPhysicsAsset.SkeletalBodySetups
						select x.Load<USkeletalBodySetup>() into x
						where x != null
						select x.BoneName.Text).Distinct().ToArray();
				}
			}
			catch (Exception ex)
			{
				warnings.Add(source.Name + ": physics attachment data unavailable: " + ex.Message);
			}
		}
		IEnumerable<string> source3 = (from i in Enumerable.Range(0, dto.Materials.Length)
			select $"mesh{number}_mat{i}").Append($"mesh{number}_fallback");
		if (!string.IsNullOrWhiteSpace(overlayMaterial))
		{
			source3 = source3.Append(overlayMaterial);
		}
		List<Part> parts = Parts;
		string name = $"{Installation.SafeName(source.Name)}_{number}";
		float[][] positions = lod.Vertices.Select((T v) => new float[3]
		{
			v.Position.X,
			v.Position.Y,
			v.Position.Z
		}).ToArray();
		float[][] normals = lod.Vertices.Select((T v) => new float[3]
		{
			v.Normal.X,
			v.Normal.Y,
			v.Normal.Z
		}).ToArray();
        if (attachment != null)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                var value = ObjExporter.ApplyTransform(new System.Numerics.Vector3(p[0], p[1], p[2]), attachment.Transform);
                positions[i] = [value.X, value.Y, value.Z];
                var n = normals[i];var q = attachment.Transform.Quaternion;var scale = attachment.Transform.Scale;
                var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Transform(new System.Numerics.Vector3(n[0]/scale[0], n[1]/scale[1], n[2]/scale[2]), new System.Numerics.Quaternion(q[0],q[1],q[2],q[3])));
                normals[i] = [normal.X, normal.Y, normal.Z];
            }
        }
		float[][] uVs = lod.Vertices.Select((T v) => new float[2]
		{
			v.Uv.U,
			1f - v.Uv.V
		}).ToArray();
		uint[][] faces = list.ToArray();
		int[] faceMaterials = list2.ToArray();
		string[] materials = source3.ToArray();
		Bone[] bones2 = bones;
		bool rigidRoot = flag;
		string[] physicsBones = array2;
		float[][][] extraUVs = lod.ExtraUvs.Select((FMeshUVFloat[] uv) => uv.Select((FMeshUVFloat v) => new float[2]
		{
			v.U,
			1f - v.V
		}).ToArray()).ToArray();
		MeshVertexColorDto[] vertexColors = lod.VertexColors;
		parts.Add(new Part(name, positions, normals, uVs, faces, faceMaterials, materials, bones2, array, rigidRoot, transform, physicsBones, extraUVs, (vertexColors != null && vertexColors.Length > 0) ? lod.VertexColors[0].Colors.Select((FColor c) => new float[4]
		{
			(float)(int)c.R / 255f,
			(float)(int)c.G / 255f,
			(float)(int)c.B / 255f,
			(float)(int)c.A / 255f
		}).ToArray() : null, overlayMaterial));
	}

	public void Write(string folder)
	{
		File.WriteAllText(Path.Combine(folder, "scene.json"), JsonSerializer.Serialize(new { Parts }));
	}
}
