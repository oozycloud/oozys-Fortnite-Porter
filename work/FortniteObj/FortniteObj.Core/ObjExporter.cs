using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse_Conversion.Dto;
using CUE4Parse_Conversion.Options;

namespace FortniteObj.Core;

public static class ObjExporter
{
	private static string F(float value)
	{
		return value.ToString("G9", CultureInfo.InvariantCulture);
	}

	public static ExportResult Export(List<UObject> meshes, Cosmetic cosmetic, string folder, List<string> warnings, IProgress<string> progress, CancellationToken ct, Dictionary<string, List<UMaterialInterface>> overrides, bool onlyBlend = false, bool matteMaterials = true, IReadOnlyList<RigSceneExporter.SceneTransform?>? transforms = null, IReadOnlyList<USplineMeshComponent?>? splines = null, Dictionary<string, StyleMaterialParameters>? parameterOverrides = null, Dictionary<string, UMaterialInterface>? overlayMaterials = null, bool createBlend = true, Dictionary<string, RigSceneExporter.Attachment>? attachments = null)
	{
		string text = Installation.SafeName(cosmetic.Name);
		MaterialExporter materials = new MaterialExporter(folder, warnings, ct, matteMaterials);
		List<object> geometry = new List<object>();
		RigSceneExporter rigScene = new RigSceneExporter();
		int offset = 0;
		int num = 0;
		StreamWriter obj = new StreamWriter(Path.Combine(folder, text + ".obj"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		try
		{
			StreamWriter mtl = new StreamWriter(Path.Combine(folder, text + ".mtl"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			try
			{
				obj.WriteLine("# Fortnite OBJ export: metres, Y up, static reference pose.");
				obj.WriteLine("mtllib " + text + ".mtl");
				foreach (UObject mesh3 in meshes)
				{
					ct.ThrowIfCancellationRequested();
					progress.Report($"Exporting mesh {num + 1}/{meshes.Count}: {mesh3.Name}");
					if (mesh3 is USkeletalMesh mesh)
					{
						using SkeletalMeshDto dto = new SkeletalMeshDto(mesh, EMeshQuality.Highest, ENaniteMeshFormat.NoNanite, exportMorphTarget: false);
						WriteMesh<SkinnedMeshVertex>(dto, mesh3.Name, num, transforms?.ElementAtOrDefault(num), overlayMaterials?.GetValueOrDefault(mesh3.GetPathName()));
					}
					else if (mesh3 is UStaticMesh mesh2)
					{
						EMeshQuality quality = (cosmetic.Category.Equals("Terrain", StringComparison.OrdinalIgnoreCase) ? EMeshQuality.All : EMeshQuality.Highest);
						using StaticMeshDto dto2 = new StaticMeshDto(mesh2, quality, ENaniteMeshFormat.NaniteLast, splines?.ElementAtOrDefault(num));
						WriteMesh<MeshVertex>(dto2, mesh3.Name, num, transforms?.ElementAtOrDefault(num), overlayMaterials?.GetValueOrDefault(mesh3.GetPathName()));
					}
					else if (mesh3 is ALandscapeProxy landscape)
					{
						using LandscapeMeshDto dto3 = new LandscapeMeshDto(landscape);
						WriteMesh<MeshVertex>(dto3, mesh3.Name, num, transforms?.ElementAtOrDefault(num), overlayMaterials?.GetValueOrDefault(mesh3.GetPathName()));
					}
					else if (mesh3 is ULandscapeComponent component)
					{
						using LandscapeMeshDto dto4 = new LandscapeMeshDto(component);
						WriteMesh<MeshVertex>(dto4, mesh3.Name, num, transforms?.ElementAtOrDefault(num), overlayMaterials?.GetValueOrDefault(mesh3.GetPathName()));
					}
					num++;
				}
			}
			finally
			{
				if (mtl != null)
				{
					((IDisposable)mtl).Dispose();
				}
			}
			void WriteMesh<T>(MeshDto<T> meshDto, string name, int number, RigSceneExporter.SceneTransform? transform, UMaterialInterface? overlayMaterial) where T : struct, IMeshVertex
			{
				MeshLodDto<T> lod = (from l in meshDto.LODs
					where l.Vertices.Length != 0 && l.Indices.Length != 0
					orderby (long)l.Vertices.Length * (long)Math.Max(1, l.Indices.Length) descending, l.Vertices.Length descending
					select l).FirstOrDefault() ?? throw new IOException(name + " has no locally available mesh geometry.");
				geometry.Add(new
				{
					Name = name,
					SourceLodIndex = lod.SourceLodIndex,
					IsNanite = lod.IsNanite,
					Vertices = lod.Vertices.Length,
					Triangles = lod.Indices.Length / 3
				});
				if (lod.SourceLodIndex != 0)
				{
					warnings.Add($"{name}: highest available geometry is LOD {lod.SourceLodIndex}; LOD 0 is missing.");
				}
				string text2 = ((overlayMaterial == null) ? null : $"mesh{number}_outer");
                var attachment = attachments?.GetValueOrDefault(meshes[number].GetPathName());
                rigScene.Add(meshes[number], meshDto, lod, number, warnings, transform, text2, attachment);
                transform ??= attachment?.Transform;
				obj.WriteLine($"o {Installation.SafeName(name)}_{number}");
				T[] vertices = lod.Vertices;
				for (int num3 = 0; num3 < vertices.Length; num3++)
				{
					T val = vertices[num3];
					Vector3 vector = ApplyTransform(val.Position, transform);
					obj.WriteLine($"v {F(vector.X / 100f)} {F(vector.Z / 100f)} {F(vector.Y / 100f)}");
				}
				vertices = lod.Vertices;
				for (int num3 = 0; num3 < vertices.Length; num3++)
				{
					T val2 = vertices[num3];
					obj.WriteLine("vt " + F(val2.Uv.U) + " " + F(1f - val2.Uv.V));
				}
				vertices = lod.Vertices;
				for (int num3 = 0; num3 < vertices.Length; num3++)
				{
					T val3 = vertices[num3];
					Vector3 vector2 = new Vector3(val3.Normal.X, val3.Normal.Y, val3.Normal.Z);
					if ((object)transform != null)
					{
						float[] quaternion = transform.Quaternion;
						Vector3 vector3 = new Vector3(transform.Scale[0], transform.Scale[1], transform.Scale[2]);
						if (Math.Abs(vector3.X * vector3.Y * vector3.Z) < 1E-12f)
						{
							throw new IOException("A scene instance has zero scale.");
						}
						vector2 = Vector3.Normalize(Vector3.Transform(vector2 / vector3, new Quaternion(quaternion[0], quaternion[1], quaternion[2], quaternion[3])));
					}
					obj.WriteLine($"vn {F(vector2.X)} {F(vector2.Z)} {F(vector2.Y)}");
				}
				for (int num4 = 0; num4 < meshDto.Materials.Length; num4++)
				{
					ct.ThrowIfCancellationRequested();
					string text3 = $"mesh{number}_mat{num4}";
					mtl.WriteLine("newmtl " + text3 + "\nKd 1 1 1\nKa 0 0 0\n" + (matteMaterials ? "Ks 0.15 0.15 0.15\nPr 0.72\nPm 0\nNs 2\n" : "Ks 0.5 0.5 0.5\nPr 0.5\n") + "illum 2");
					try
					{
						UMaterialInterface uMaterialInterface = meshDto.Materials[num4].Material?.Load() as UMaterialInterface;
						List<UMaterialInterface> list = overrides.GetValueOrDefault("instance:" + number) ?? overrides.GetValueOrDefault(meshes[number].GetPathName());
						UMaterialInterface uMaterialInterface2 = overrides.GetValueOrDefault($"slot:{meshes[number].GetPathName()}:{num4}")?.FirstOrDefault() ?? ((list != null && num4 < list.Count) ? list[num4] : null) ?? uMaterialInterface;
						UMaterialInterface uMaterialInterface3 = ((uMaterialInterface2 == null) ? null : overrides.GetValueOrDefault("material:" + uMaterialInterface2.GetPathName())?.FirstOrDefault()) ?? uMaterialInterface2;
						if (uMaterialInterface3 != null)
						{
							materials.Write(mtl, text3, uMaterialInterface3, parameterOverrides?.GetValueOrDefault("instance:" + number) ?? parameterOverrides?.GetValueOrDefault(uMaterialInterface3.GetPathName()));
						}
						else
						{
							warnings.Add($"{name}: material slot {num4} is unavailable.");
						}
					}
					catch (Exception ex) when (!(ex is OperationCanceledException))
					{
						warnings.Add($"{name}, material {num4}: {ex.Message}");
					}
					mtl.WriteLine();
				}
				mtl.WriteLine($"newmtl mesh{number}_fallback\nKd 0.7 0.7 0.7\nKs 0.15 0.15 0.15\nPr 0.72\nPm 0\nNs 2\nillum 1\n");
				if (overlayMaterial != null)
				{
					try
					{
						materials.Write(mtl, text2, overlayMaterial, parameterOverrides?.GetValueOrDefault(overlayMaterial.GetPathName()));
					}
					catch (Exception ex2) when (!(ex2 is OperationCanceledException))
					{
						warnings.Add(name + ": toon outer-layer material unavailable: " + ex2.Message);
					}
					mtl.WriteLine();
				}
				MeshSectionDto[] sections = lod.Sections;
				for (int num3 = 0; num3 < sections.Length; num3++)
				{
					MeshSectionDto meshSectionDto = sections[num3];
					ct.ThrowIfCancellationRequested();
					string text4 = ((meshSectionDto.MaterialIndex >= 0 && meshSectionDto.MaterialIndex < meshDto.Materials.Length) ? $"mesh{number}_mat{meshSectionDto.MaterialIndex}" : $"mesh{number}_fallback");
					obj.WriteLine("usemtl " + text4);
					int num5 = checked(meshSectionDto.FirstIndex + meshSectionDto.NumFaces * 3);
					if (meshSectionDto.FirstIndex < 0 || num5 > lod.Indices.Length)
					{
						throw new IOException("Mesh section has invalid triangle indices.");
					}
					for (int num6 = meshSectionDto.FirstIndex; num6 < num5; num6 += 3)
					{
						int value = Index(lod.Indices[num6]);
						int num7 = Index(lod.Indices[num6 + 1]);
						int num8 = Index(lod.Indices[num6 + 2]);
						if ((object)transform != null && transform.Scale[0] * transform.Scale[1] * transform.Scale[2] < 0f)
						{
							int num9 = num8;
							num8 = num7;
							num7 = num9;
						}
						obj.WriteLine($"f {value}/{value}/{value} {num7}/{num7}/{num7} {num8}/{num8}/{num8}");
					}
				}
				offset += lod.Vertices.Length;
				int Index(uint index)
				{
					if (index >= lod.Vertices.Length)
					{
						throw new IOException("Mesh vertex index is out of range.");
					}
					return checked(offset + (int)index + 1);
				}
			}
		}
		finally
		{
			if (obj != null)
			{
				((IDisposable)obj).Dispose();
			}
		}
		rigScene.Write(folder);
		File.WriteAllText(Path.Combine(folder, "materials.json"), JsonSerializer.Serialize(new
		{
			Obj = text + ".obj",
			Blend = text + ".blend",
			Scene = "scene.json",
			RigName = text + "_Rig",
			MatteMaterials = matteMaterials,
			Materials = materials.Materials
		}, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		if (createBlend)
		{
			BlenderExport.Create(folder, warnings, progress, ct);
		}
		int num2 = materials.Textures.Count();
		if (num2 == 0)
		{
			warnings.Add("No textures were exported. Geometry is available, but this is not a complete textured export.");
		}
		ExportResult result = new ExportResult(folder, num, num2, warnings.Distinct().ToArray());
		File.WriteAllText(Path.Combine(folder, "export-report.json"), JsonSerializer.Serialize(new
		{
			Cosmetic = cosmetic,
			Geometry = geometry,
			RigParts = rigScene.Parts.Select((RigSceneExporter.Part p) => new
			{
				Name = p.Name,
				Bones = p.Bones.Length,
				WeightedVertices = p.Weights.Length,
				RigidRoot = p.RigidRoot
			}),
			Textures = materials.Textures,
			NormalConvention = "OpenGL",
			Result = result
		}, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		if (onlyBlend && !File.Exists(Path.Combine(folder, text + ".blend")))
		{
			throw new IOException("Blender did not create a .blend file. Install Blender or disable Only .blend. Intermediate files were retained.");
		}
		if (onlyBlend)
		{
			GameSession.CleanIntermediates(folder);
		}
		return result;
	}

	internal static Vector3 ApplyTransform(Vector3 position, RigSceneExporter.SceneTransform? transform)
	{
		if ((object)transform == null)
		{
			return position;
		}
		Vector3 vector = new Vector3(transform.Scale[0], transform.Scale[1], transform.Scale[2]);
		float[] array = transform.Quaternion ?? throw new IOException("Scene rotation is missing.");
		return Vector3.Transform(rotation: new Quaternion(array[0], array[1], array[2], array[3]), value: position * vector) + new Vector3(transform.Location[0], transform.Location[1], transform.Location[2]);
	}
}
