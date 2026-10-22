using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.GameTypes.FN.Assets.Exports;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Dto;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using Newtonsoft.Json;
using SkiaSharp;

namespace FortniteObj.Core;

public sealed partial class GameSession : IDisposable
{
	private record AnimationTrack(string Bone, List<float[]> Frames);

	private record PartMesh(UObject Part, UObject Mesh);

	private sealed record PrefabPlacement(string ActorClass, FTransform Transform, StyleMaterialParameters? Parameters = null, UMaterialInterface? Material = null);

	private sealed record MapMeshInstance(UObject Mesh, RigSceneExporter.SceneTransform Transform, UStaticMeshComponent? Component = null, FPackageIndex[]? Materials = null, StyleMaterialParameters? Parameters = null, UMaterialInterface? SavedMaterial = null);

	private QualityFileProvider? _provider;

	public List<string> Warnings { get; } = new List<string>();

	public int FileCount => _provider?.Files.Count ?? 0;

	public static IReadOnlyList<Cosmetic> BuiltInSkyboxes { get; } = new string[5] { "Daylight", "Sunrise", "Sunset", "Overcast", "Night" }.Select((string name) => new Cosmetic("SKYBOX_" + name.ToUpperInvariant(), name + " sky", "Skyboxes", "__SKYBOX_" + name.ToUpperInvariant() + "__", "Built-in skybox")).ToArray();

	public bool IsTraversalEmote(Cosmetic emote)
	{
		if (emote.Category == "Animations" && _provider != null)
		{
			return _provider.LoadPackageObject(emote.PackagePath ?? emote.ObjectPath).GetOrDefault("bMovingEmote", defaultValue: false);
		}
		return false;
	}

	private void WriteAnimation(Cosmetic emote, string folder, IProgress<string> progress, CancellationToken ct, Cosmetic? skin = null, double animationFps = 0.0, string emoteStyle = "Default")
	{
		UObject uObject = _provider.LoadPackageObject(emote.PackagePath ?? emote.ObjectPath);
		List<string> list = new List<string>();
		FPropertyTag fPropertyTag = uObject.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "Animation") ?? throw new IOException("This emote has no skeletal animation (it may be a spray or emoticon).");
		if ((object)skin != null)
		{
			string? text = TextValue(_provider.LoadPackageObject(skin.PackagePath ?? skin.ObjectPath).Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "Gender")?.Tag?.GenericValue);
			if (text != null && text.EndsWith("::Female", StringComparison.OrdinalIgnoreCase))
			{
				fPropertyTag = uObject.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "AnimationFemaleOverride" && SoftPaths(p.Tag?.GenericValue).Any((FSoftObjectPath x) => !string.IsNullOrEmpty(x.AssetPathName.Text))) ?? fPropertyTag;
			}
		}
		UAnimationAsset asset = ReferencedObjects(fPropertyTag.Tag?.GenericValue, list).OfType<UAnimationAsset>().FirstOrDefault() ?? throw new IOException("The emote's animation could not be loaded. " + string.Join("; ", list));
		progress.Report("Decoding animation tracks: " + emote.Name);
		CAnimSet set = asset.ConvertAnims();
		bool orDefault = uObject.GetOrDefault("bMovingEmote", defaultValue: false);
		bool hasMotion = set.Sequences.Any((CAnimSequence s) => EmoteVariants.IsMotion(s.Name, s.SlotName?.ToString()));
		bool hasStatic = set.Sequences.Any((CAnimSequence s) => EmoteVariants.IsStatic(s.Name));
		FReferenceSkeleton skeleton = set.Skeleton.ReferenceSkeleton;
		RigSceneExporter.Bone[] array = skeleton.FinalRefBoneInfo.Select((FMeshBoneInfo b, int i) =>
		{
			FTransform fTransform2 = skeleton.FinalRefBonePose[i];
			return new RigSceneExporter.Bone(b.Name.Text, b.ParentIndex, new float[3]
			{
				fTransform2.Translation.X,
				fTransform2.Translation.Y,
				fTransform2.Translation.Z
			}, new float[4]
			{
				fTransform2.Rotation.X,
				fTransform2.Rotation.Y,
				fTransform2.Rotation.Z,
				fTransform2.Rotation.W
			}, new float[3]
			{
				fTransform2.Scale3D.X,
				fTransform2.Scale3D.Y,
				fTransform2.Scale3D.Z
			}, (i < set.Skeleton.BoneTree.Length) ? set.Skeleton.BoneTree[i].ToString() : "Skeleton");
		}).ToArray();
		List<object> list2 = new List<object>();
		foreach (CAnimSequence sequence in set.Sequences)
		{
			ct.ThrowIfCancellationRequested();
			if ((orDefault && !EmoteVariants.Include(sequence.Name, sequence.SlotName?.ToString(), emoteStyle, hasMotion, hasStatic)) || sequence.NumFrames < 2)
			{
				continue;
			}
			if (sequence.IsAdditive)
			{
				list.Add(sequence.Name + ": additive clip skipped");
				continue;
			}
			List<AnimationTrack> list3 = new List<AnimationTrack>();
			FTransform[] reference = sequence.RetargetBasePose ?? skeleton.FinalRefBonePose;
			RigSceneExporter.Bone[] bones = array.Select((RigSceneExporter.Bone b, int i) => (i >= reference.Length) ? b : b with
			{
				Translation = new float[3]
				{
					reference[i].Translation.X,
					reference[i].Translation.Y,
					reference[i].Translation.Z
				},
				Rotation = new float[4]
				{
					reference[i].Rotation.X,
					reference[i].Rotation.Y,
					reference[i].Rotation.Z,
					reference[i].Rotation.W
				},
				Scale = new float[3]
				{
					reference[i].Scale3D.X,
					reference[i].Scale3D.Y,
					reference[i].Scale3D.Z
				}
			}).ToArray();
			for (int num = 0; num < Math.Min(sequence.Tracks.Count, array.Length); num++)
			{
				CAnimTrack cAnimTrack = sequence.Tracks[num];
				if (cAnimTrack.HasKeys())
				{
					List<float[]> list4 = new List<float[]>();
					for (int num2 = 0; num2 < sequence.NumFrames; num2++)
					{
						ct.ThrowIfCancellationRequested();
						FTransform fTransform = ((reference.Length > num) ? reference[num] : skeleton.FinalRefBonePose[num]);
						FQuat dstQuat = fTransform.Rotation;
						FVector dstPos = fTransform.Translation;
						FVector dstScale = fTransform.Scale3D;
						cAnimTrack.GetBoneTransform(num2, sequence.NumFrames, ref dstQuat, ref dstPos, ref dstScale);
						list4.Add(new float[10] { dstPos.X, dstPos.Y, dstPos.Z, dstQuat.X, dstQuat.Y, dstQuat.Z, dstQuat.W, dstScale.X, dstScale.Y, dstScale.Z });
					}
					list3.Add(new AnimationTrack(array[num].Name, list4));
				}
			}
			float duration = sequence.OriginalSequence.SequenceLength / Math.Max(0.001f, sequence.OriginalSequence.RateScale);
			float authoredFps = ((duration > 0f) ? ((float)(sequence.NumFrames - 1) / duration) : 30f);
			double outputFps = ((animationFps > 0.0 && double.IsFinite(animationFps)) ? Math.Clamp(animationFps, 1.0, 240.0) : ((double)authoredFps));
			double[] times = ((animationFps > 0.0) ? (from f in Enumerable.Range(0, Math.Max(2, (int)Math.Ceiling((double)duration * outputFps - 0.001) + 1))
				select Math.Min(duration, (double)f / outputFps)).ToArray() : (from f in Enumerable.Range(0, sequence.NumFrames)
				select (double)f / (double)authoredFps).ToArray());
			times[^1] = duration;
			if (animationFps > 0.0)
			{
				list3 = list3.Select((AnimationTrack track) => new AnimationTrack(track.Bone, times.Select((double t) => InterpolateFrame(track.Frames, (duration > 0f) ? (t / (double)duration * (double)(sequence.NumFrames - 1)) : 0.0)).ToList())).ToList();
			}
			list2.Add(new
			{
				Name = sequence.Name,
				Frames = times.Length,
				FrameTimes = times,
				Fps = outputFps,
				Duration = duration,
				AuthoredFps = authoredFps,
				Bones = bones,
				Tracks = list3,
				SlotName = sequence.SlotName
			});
		}
		if (list2.Count == 0)
		{
			throw new IOException(orDefault ? ("This emote has no playable " + emoteStyle + " variant. Try the other traversal style.") : "No playable non-additive animation tracks could be decoded for this emote.");
		}
		string path = Path.Combine(folder, "animation.json");
		string name = emote.Name;
		string style;
		if (orDefault)
		{
			style = ((emoteStyle == "Static traversal") ? "Static traversal" : "Traversal");
		}
		else
		{
			style = "Default";
		}
		File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
		{
			Name = name,
			Style = style,
			Bones = array,
			Clips = list2,
			Warnings = list
		}));
		progress.Report($"Decoded {list2.Count} animation clips with bone keyframes.");
	}

	internal static float[] InterpolateFrame(List<float[]> frames, double sourceFrame)
	{
		sourceFrame = Math.Clamp(sourceFrame, 0.0, frames.Count - 1);
		int num = (int)Math.Floor(sourceFrame);
		int index = Math.Min(num + 1, frames.Count - 1);
		float[] array = frames[num];
		float[] array2 = frames[index];
		float fraction = (float)(sourceFrame - (double)num);
		float[] array3 = array.Zip(array2, (float x, float y) => x + (y - x) * fraction).ToArray();
		Quaternion quaternion = new Quaternion(array[3], array[4], array[5], array[6]);
		Quaternion quaternion2 = new Quaternion(array2[3], array2[4], array2[5], array2[6]);
		Quaternion quaternion3 = Quaternion.Normalize(Quaternion.Slerp(quaternion, quaternion2, fraction));
		array3[3] = quaternion3.X;
		array3[4] = quaternion3.Y;
		array3[5] = quaternion3.Z;
		array3[6] = quaternion3.W;
		return array3;
	}

	public async Task<List<Cosmetic>> Scan(AppSettings settings, IProgress<string> progress, CancellationToken ct)
	{
		Dispose();
		Warnings.Clear();
		string paks = Installation.Paks(settings.GameFolder);
		CompatibilityData compatibility = await Compatibility.Prepare(settings, progress, ct);
		FortniteGgCatalog fortniteGg = await FortniteGgCatalog.LoadAsync(progress, ct);
		if (!Enum.TryParse<EGame>(settings.EngineVersion, out var game))
		{
			throw new ArgumentException("Choose a supported engine version.");
		}
		return await Task.Run(() =>
		{
			progress.Report("Mounting local Fortnite archives…");
			_provider = new QualityFileProvider(paks, new VersionContainer(game, optionOverrides: new Dictionary<string, bool>
            {
                // Fortnite cooks this field even when its configuration INI is absent.
                ["SkeletalMesh.KeepMobileMinLODSettingOnDesktop"] = true
            }))
			{
				MappingsContainer = new CurrentUsmapProvider(compatibility.MappingPath),
				DownloadMissing = true,
				Progress = progress
			};
			_provider.Initialize();
			ct.ThrowIfCancellationRequested();
			_provider.SubmitKeys(compatibility.Keys);
			_provider.PostMount();
			if (_provider.Files.Count == 0)
			{
				throw new IOException("No archives could be opened. This build may need different AES keys.");
			}
			progress.Report($"Opened {_provider.Files.Count:N0} files. Reading cosmetic definitions…");
			try
			{
				_provider.LoadVirtualPaths();
			}
			catch (Exception ex)
			{
				Warnings.Add("Virtual paths: " + ex.Message);
			}
			try
			{
				_provider.LoadLocalization(ELanguage.English, ct);
			}
			catch (Exception ex2) when (!(ex2 is OperationCanceledException))
			{
				Warnings.Add("Names: " + ex2.Message);
			}
			GameFile[] array = _provider.Files.Values.Where((GameFile f) => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && IsCandidate(f.Path)).ToArray();
			List<Cosmetic> list = new List<Cosmetic>();
			int num = 0;
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				ct.ThrowIfCancellationRequested();
				GameFile gameFile = array[num2];
				try
				{
					foreach (UObject export in _provider.LoadPackage(gameFile.Path).GetExports())
					{
						string text = export.ExportType switch
						{
							"FortWeaponRangedItemDefinition" => "Weapons",
                            "FortWeaponMeleeItemDefinition" => "Weapons",
                            "FortVehicleItemDefinition" => "Vehicles",
                            "BlueprintGeneratedClass" when IsBuildPackage(gameFile.Path) => "Builds",
                            "AthenaCharacterItemDefinition" => "Skins", 
							"AthenaPickaxeItemDefinition" => "Pickaxes", 
							"AthenaBackpackItemDefinition" => "Back blings", 
							"AthenaGliderItemDefinition" => "Gliders", 
							"AthenaDanceItemDefinition" => "Animations", 
							"AthenaEmojiItemDefinition" => "Emoticons", 
							"AthenaSprayItemDefinition" => "Sprays", 
							"AthenaLoadingScreenItemDefinition" => "Loading screens", 
							"CosmeticShoesItemDefinition" => "Kicks", 
							"AthenaItemWrapDefinition" => "Wraps", 
							"AthenaPetCarrierItemDefinition" => "Pet carriers", 
							"AthenaPetItemDefinition" => "Pets", 
							"AthenaToyItemDefinition" => "Toys", 
							"AthenaBattleBusItemDefinition" => "Battle buses", 
							"CosmeticCompanionItemDefinition" => "Sidekicks", 
							"FortPlaysetItemDefinition" => "Prefabs", 
							_ => null, 
						};
						if (text == "Weapons" && (export.Name.Contains("Harvest", StringComparison.OrdinalIgnoreCase) || export.Name.Contains("Pickaxe", StringComparison.OrdinalIgnoreCase))) continue;
                        if (text != null && (!(text == "Loading screens") || export.Properties.Any((FPropertyTag p) => p.Name.Text == "BackgroundImage")) && (!(text == "Wraps") || export.Properties.Any((FPropertyTag p) => p.Name.Text == "ItemWrapMaterial")) && (!(text == "Prefabs") || JsonConvert.SerializeObject(export.Properties.Where((FPropertyTag p) => p.Name.Text == "CreativeTagsHelper")).Contains("Prefab", StringComparison.OrdinalIgnoreCase)))
						{
							string text2 = (export.GetOrDefault<FText>("ItemName") ?? export.GetOrDefault<FText>("DisplayName"))?.Text;
							text2 = AssetDisplayName(export, text, text2);
                            FSoftObjectPath[] array2 = SoftPaths(export.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "BaseCharacterParts")?.Tag?.GenericValue).ToArray();
							FSoftObjectPath[] array3 = array2.Where((FSoftObjectPath p) => !_provider.TryGetGameFile(p.AssetPathName.Text.Split('.')[0], out GameFile _)).ToArray();
							string availability = ((array2.Length == 0) ? "Check on export" : ((array3.Length == 0) ? "Parts local" : (array3.All((FSoftObjectPath p) => _provider.StreamContainer(p.AssetPathName.Text) != null) ? "Streamed parts" : "Not indexed")));
							string text3;
							if (!gameFile.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
							{
								text3 = gameFile.Path;
							}
							else
							{
								string path = gameFile.Path;
								text3 = path.Substring(0, path.Length - 7);
							}
							string packagePath = text3;
							list.Add(new Cosmetic(export.Name, string.IsNullOrWhiteSpace(text2) ? export.Name : text2, text, export.GetPathName(), availability, packagePath));
						}
					}
				}
				catch (Exception ex3)
				{
					num++;
					if (Warnings.Count < 30)
					{
						Warnings.Add(gameFile.Path + ": " + ex3.Message);
					}
				}
				if (num2 % 250 == 0)
				{
					progress.Report($"Reading cosmetics {num2 + 1:N0}/{array.Length:N0} · found {list.Count:N0} · unreadable {num:N0}");
				}
			}
			if (list.Count == 0)
			{
				throw new IOException($"No skins or pickaxes could be decoded from {array.Length:N0} candidates ({num:N0} failed). Check the engine version and mappings.\n" + Warnings.FirstOrDefault());
			}
			if (num > 0)
			{
				Warnings.Add($"{num:N0} cosmetic packages could not be read; the list may be incomplete.");
			}
			list.AddRange(BuiltInSkyboxes);
			progress.Report("Loading catalog thumbnails…");
			list = (from x in list.Select(fortniteGg.Enrich)
				select LocalPreview(x, ct)).ToList();
			progress.Report($"Ready · {list.Count:N0} assets across {list.Select((Cosmetic c) => c.Category).Distinct().Count()} categories" + ((num > 0) ? $" · {num:N0} unreadable packages" : ""));
			return list.DistinctBy((Cosmetic x) => x.ObjectPath).OrderBy((Cosmetic x) => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
		}, ct);
	}

	public static bool IsCandidate(string path)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (IsBuildPackage(path) || fileNameWithoutExtension.StartsWith("WID_", StringComparison.OrdinalIgnoreCase) || fileNameWithoutExtension.StartsWith("VID_", StringComparison.OrdinalIgnoreCase)) return true;
		if (!fileNameWithoutExtension.StartsWith("Companion_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Shoes_", StringComparison.OrdinalIgnoreCase) && !new string[5] { "ItemWraps", "PetCarriers", "Pets", "Toys", "BattleBuses" }.Any((string category) => path.Contains("/Items/Cosmetics/" + category + "/", StringComparison.OrdinalIgnoreCase)) && !path.Contains("/Items/Cosmetics/Sprays/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/Items/Cosmetics/LoadingScreens/", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("PID_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Emoji_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("CID_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Character_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Pickaxe_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("BID_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Backpack_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("Glider_", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("EID_", StringComparison.OrdinalIgnoreCase) && !path.Contains("/Items/Cosmetics/Characters/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/Items/Cosmetics/Pickaxes/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/Items/Cosmetics/Backpacks/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/Items/Cosmetics/Gliders/", StringComparison.OrdinalIgnoreCase))
		{
			return path.Contains("/Items/Cosmetics/Dances/", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private IEnumerable<FStructFallback> Structures(object? value, int depth = 0)
	{
		if (depth > 24)
		{
			yield break;
		}
		if (value is FStructFallback fStructFallback)
		{
			yield return fStructFallback;
		}
		foreach (object item in Elements(value))
		{
			foreach (FStructFallback item2 in Structures(item, depth + 1))
			{
				yield return item2;
			}
		}
	}

	private List<FStructFallback> StyleOptions(Cosmetic cosmetic)
	{
		return (from o in _provider.LoadPackage(cosmetic.PackagePath ?? cosmetic.ObjectPath.Split('.')[0]).GetExports().SelectMany((UObject o) => Structures(o.Properties))
			where o.Properties.Any((FPropertyTag p) => p.Name.Text == "VariantName")
			select o).ToList();
	}

	public List<string> GetStyles(Cosmetic cosmetic)
	{
		if (_provider == null)
		{
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "Default";
			return list;
		}
		if (cosmetic.Category == "Animations")
		{
			int num;
			if (!IsTraversalEmote(cosmetic))
			{
				num = 1;
				List<string> list2 = new List<string>(num);
				CollectionsMarshal.SetCount(list2, num);
				CollectionsMarshal.AsSpan(list2)[0] = "Default";
				return list2;
			}
			num = 2;
			List<string> list3 = new List<string>(num);
			CollectionsMarshal.SetCount(list3, num);
			Span<string> span = CollectionsMarshal.AsSpan(list3);
			span[0] = "Traversal";
			span[1] = "Static traversal";
			return list3;
		}
		bool flag;
		switch (cosmetic.Category)
		{
		case "Terrain":
		case "Skyboxes":
		case "Prefabs":
		case "Emoticons":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			int num = 1;
			List<string> list4 = new List<string>(num);
			CollectionsMarshal.SetCount(list4, num);
			CollectionsMarshal.AsSpan(list4)[0] = "Default";
			return list4;
		}
		return new string[1] { "Default" }.Concat(from o in StyleOptions(cosmetic)
			select TextValue(o.Properties.First((FPropertyTag p) => p.Name.Text == "VariantName").Tag?.GenericValue) ?? "Unnamed style").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public ExportResult Export(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, string styleName = "Default", bool onlyBlend = false, bool matteMaterials = true, double animationFps = 0.0, bool createBlend = true)
	{
		if (cosmetic.Category == "Skyboxes")
		{
			return ExportSkybox(cosmetic, root, progress, ct, onlyBlend);
		}
		if (cosmetic.Category == "Terrain")
		{
			throw new ArgumentException("Terrain exports have been removed.");
		}
		if (_provider == null)
		{
			throw new InvalidOperationException("Scan your installation first.");
		}
		_provider.BeginExport(ct);
		_provider.Progress = progress;
		_provider.EnablePackageStreaming = true;
		string text = cosmetic.PackagePath ?? cosmetic.ObjectPath;
		_provider.EnsurePackage(text);
		if (cosmetic.Category == "Animations")
		{
			ExportResult exportResult = ExportAnimation(cosmetic, root, progress, ct, animationFps, styleName);
			if (onlyBlend)
			{
				CleanIntermediates(exportResult.Folder);
			}
			return exportResult;
		}
		if (cosmetic.Category == "Emoticons")
		{
			return ExportEmoticon(cosmetic, root, progress, ct, onlyBlend);
		}
		string category = cosmetic.Category;
		if ((category == "Sprays" || category == "Loading screens") ? true : false)
		{
			return ExportImageCosmetic(cosmetic, root, progress, ct, onlyBlend);
		}
		if (cosmetic.Category == "Wraps")
		{
			return ExportWrap(cosmetic, root, progress, ct, onlyBlend, matteMaterials);
		}
		if (cosmetic.Category == "Prefabs")
		{
			return ExportPrefab(cosmetic, root, progress, ct, onlyBlend, matteMaterials);
		}
		if (cosmetic.Category is "Weapons" or "Vehicles" or "Builds")
            return ExportGameAsset(cosmetic, root, progress, ct, onlyBlend, matteMaterials);
        UObject uObject = _provider.LoadPackageObject(text);
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<UObject> list3 = ResolveMeshes(uObject, list, list2, ct);
		ct.ThrowIfCancellationRequested();
		if (list2.Count > 0)
		{
			throw new IOException($"This item's default mesh references could not all be loaded ({list2.Count} unresolved). A complete export could not be made.\n\n" + string.Join("\n", list2.Take(4)) + "\n\nStreamed asset loading is automatic. Check the log for download or compatibility errors. This status does not check whether you own the skin.");
		}
		if (list3.Count == 0)
		{
			throw new IOException("No mesh could be loaded for this item. The exporter checked local and streamed sources; see the log for unresolved references.\n" + string.Join("\n", list.Take(3)));
		}
		string text2 = Path.Combine(Path.GetFullPath(root), Installation.SafeName(cosmetic.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
		Directory.CreateDirectory(text2);
		try
		{
			Dictionary<string, List<UMaterialInterface>> dictionary = ResolveCharacterPartMaterialOverrides(uObject, list);
			MergeOverrides(dictionary, ResolveMaterialOverrides(uObject, list));
			if (!string.IsNullOrWhiteSpace(styleName) && !styleName.Equals("Default", StringComparison.OrdinalIgnoreCase))
			{
				ApplyStylePartMeshes(uObject, text, styleName, list3, list, list2, ct, dictionary);
				Dictionary<string, List<UMaterialInterface>> dictionary2 = ResolveStyleOverrides(uObject, text, styleName, list);
				ApplyStyleToCharacterSlots(dictionary, dictionary2);
				MergeOverrides(dictionary, dictionary2);
			}
			Dictionary<string, UMaterialInterface> overlayMaterials = ResolveOverlayMaterials(uObject, text, styleName, list, list2);
			if (cosmetic.Category == "Pickaxes")
			{
				WritePickaxeFx(uObject, text2, list);
			}
			if (list2.Count > 0)
			{
				throw new IOException("Selected style references could not be loaded: " + string.Join("; ", list2.Distinct()));
			}
			return ObjExporter.Export(list3, cosmetic, text2, list, progress, ct, dictionary, onlyBlend, matteMaterials, null, null, ResolveStyleParameters(text, styleName, list), overlayMaterials, createBlend, ResolvePartAttachments(uObject, text, styleName, list3, list));
		}
		catch
		{
			File.WriteAllText(Path.Combine(text2, "INCOMPLETE.txt"), "This export did not finish. Retry the export; incomplete files should not be used.");
			throw;
		}
	}

	public ExportResult ExportScene(Cosmetic skin, Cosmetic emote, string root, string sceneName, IProgress<string> progress, CancellationToken ct, string styleName = "Default", bool onlyBlend = false, bool matteMaterials = true, double animationFps = 0.0, string emoteStyle = "Default")
	{
		if (skin.Category != "Skins" || emote.Category != "Animations")
		{
			throw new ArgumentException("Choose a skin and an emote.");
		}
		ExportResult exportResult = Export(skin, root, progress, ct, styleName, onlyBlend: false, matteMaterials, 0.0, createBlend: false);
		try
		{
			string? directoryName = Path.GetDirectoryName(exportResult.Folder);
			string name = (string.IsNullOrWhiteSpace(sceneName) ? (skin.Name + "_" + emote.Name) : sceneName);
			string text = Path.Combine(directoryName, Installation.SafeName(name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
			if (!string.Equals(text, exportResult.Folder, StringComparison.OrdinalIgnoreCase))
			{
				Directory.Move(exportResult.Folder, text);
				exportResult = exportResult with
				{
					Folder = text
				};
			}
			WriteAnimation(emote, exportResult.Folder, progress, ct, skin, animationFps, emoteStyle);
			List<string> list = exportResult.Warnings.ToList();
			using (JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(exportResult.Folder, "animation.json"))))
			{
				list.AddRange(from x in jsonDocument.RootElement.GetProperty("Warnings").EnumerateArray()
					select x.GetString());
			}
			string path = Path.Combine(exportResult.Folder, Installation.SafeName(string.IsNullOrWhiteSpace(sceneName) ? (skin.Name + "_" + emote.Name) : sceneName) + ".blend");
			string path2 = Path.Combine(exportResult.Folder, "materials.json");
			JsonNode jsonNode = JsonNode.Parse(File.ReadAllText(path2));
			jsonNode["Blend"] = Path.GetFileName(path);
			jsonNode["RequireAnimation"] = true;
			File.WriteAllText(path2, jsonNode.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			BlenderExport.Create(exportResult.Folder, list, progress, ct, required: true);
			if (!File.Exists(Path.Combine(exportResult.Folder, "animation-report.json")) || !File.Exists(path))
			{
				throw new IOException("Blender did not create the animated scene. Check blender.log.");
			}
			exportResult = exportResult with
			{
				Warnings = list.Distinct().ToArray()
			};
			string path3 = Path.Combine(exportResult.Folder, "export-report.json");
			JsonNode jsonNode2 = JsonNode.Parse(File.ReadAllText(path3));
			jsonNode2["Result"] = System.Text.Json.JsonSerializer.SerializeToNode(exportResult);
			jsonNode2["Animation"] = System.Text.Json.JsonSerializer.SerializeToNode(new { emote.Name, emote.ObjectPath });
			File.WriteAllText(path3, jsonNode2.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			if (onlyBlend)
			{
				CleanIntermediates(exportResult.Folder);
			}
			return exportResult with
			{
				Warnings = list.ToArray()
			};
		}
		catch
		{
			File.WriteAllText(Path.Combine(exportResult.Folder, "INCOMPLETE.txt"), "Animation export failed; the skin may be present but the requested scene is incomplete.");
			throw;
		}
	}

	internal static void CleanIntermediates(string folder)
	{
		if (!Directory.EnumerateFiles(folder, "*.blend").Any())
		{
			throw new IOException("No Blender file was created; intermediate files were retained.");
		}
		foreach (string item in Directory.EnumerateFiles(folder))
		{
			if (!item.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) && !item.EndsWith("report.json", StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(item);
			}
		}
		foreach (string item2 in Directory.EnumerateDirectories(folder))
		{
			Directory.Delete(item2, recursive: true);
		}
	}

	private ExportResult ExportAnimation(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, double animationFps = 0.0, string emoteStyle = "Default")
	{
		string text = Path.Combine(Path.GetFullPath(root), Installation.SafeName(cosmetic.Name) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text);
		WriteAnimation(cosmetic, text, progress, ct, null, animationFps, emoteStyle);
		using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(text, "animation.json")));
		RigSceneExporter.Bone[] bones = jsonDocument.RootElement.GetProperty("Bones").Deserialize<RigSceneExporter.Bone[]>();
		File.WriteAllText(Path.Combine(text, "scene.json"), System.Text.Json.JsonSerializer.Serialize(new
		{
			Parts = new[]
			{
				new
				{
					Name = "Animation skeleton",
					Bones = bones,
					Positions = Array.Empty<float[]>(),
					Normals = Array.Empty<float[]>(),
					UVs = Array.Empty<float[]>(),
					Faces = Array.Empty<int[]>(),
					FaceMaterials = Array.Empty<int>(),
					Materials = Array.Empty<string>(),
					Weights = Array.Empty<object>(),
					RigidRoot = false
				}
			}
		}));
		File.WriteAllText(Path.Combine(text, "materials.json"), System.Text.Json.JsonSerializer.Serialize(new
		{
			Scene = "scene.json",
			Blend = Installation.SafeName(cosmetic.Name) + ".blend",
			RigName = "Emote_Rig",
			Materials = Array.Empty<object>()
		}));
		List<string> list = (from x in jsonDocument.RootElement.GetProperty("Warnings").EnumerateArray()
			select x.GetString()).ToList();
		BlenderExport.Create(text, list, progress, ct);
		if (!File.Exists(Path.Combine(text, "animation-report.json")))
		{
			throw new IOException("Animated Blender conversion failed; see blender.log.");
		}
		return new ExportResult(text, 0, 0, list.ToArray());
	}

	private static string ReadableMapName(string path)
	{
		string text = path.Replace('\\', '/');
		string[] array = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
		int num = Array.FindIndex(array, (string s) => s.Equals("_Generated_", StringComparison.OrdinalIgnoreCase));
		string text2 = ((num > 0) ? array[num - 1] : array.Reverse().Skip(1).FirstOrDefault((string s) => !s.Equals("Maps", StringComparison.OrdinalIgnoreCase)));
		if (text2 == null)
		{
			text2 = "Fortnite Map";
		}
		text2 = Regex.Replace(text2, "([a-z])([A-Z])", "$1 $2").Replace('_', ' ');
		text2 = Regex.Replace(text2, "\\s+(Terrain|Map|Level|World)$", "", RegexOptions.IgnoreCase).Trim();
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = "Fortnite Map";
		}
		string text3 = (Regex.IsMatch(fileNameWithoutExtension, "^[A-Za-z0-9]{10,}$") ? (" · Cell " + fileNameWithoutExtension.Substring(0, Math.Min(8, fileNameWithoutExtension.Length))) : "");
		return text2 + text3;
	}

	private static RigSceneExporter.SceneTransform? ReadMapTransform(UObject value)
	{
		object obj = NamedValues(value.Properties, "RelativeLocation").FirstOrDefault();
		object obj2 = NamedValues(value.Properties, "RelativeRotation").FirstOrDefault();
		object obj3 = NamedValues(value.Properties, "RelativeScale3D").FirstOrDefault();
		if (obj == null && obj2 == null && obj3 == null)
		{
			return null;
		}
		return new RigSceneExporter.SceneTransform(Vector(obj, new float[3]), Rotation(obj2), Vector(obj3, new float[3] { 1f, 1f, 1f }));
		static float ComponentFrom(object raw, string name, float defaultValue)
		{
			return (float)NumberValue(raw.GetType().GetProperty(name)?.GetValue(raw) ?? raw.GetType().GetField(name)?.GetValue(raw), defaultValue);
		}
		static object? Find(object obj5, string key)
		{
			object? obj4 = obj5.GetType().GetProperty(key)?.GetValue(obj5);
			if (obj4 == null)
			{
				FieldInfo? field = obj5.GetType().GetField(key);
				if ((object)field == null)
				{
					return null;
				}
				obj4 = field.GetValue(obj5);
			}
			return obj4;
		}
		static float[] Rotation(object? raw)
		{
			if (raw != null)
			{
				return new float[3]
				{
					Component("Pitch", "P"),
					Component("Yaw", "Y"),
					Component("Roll", "R")
				};
			}
			return new float[3];
			float Component(string name, string fallbackName)
			{
				object obj4 = Find(raw, name) ?? Find(raw, fallbackName);
				if (obj4 != null)
				{
					return (float)NumberValue(obj4, 0.0);
				}
				object obj5 = raw.GetType().GetField("ScriptStruct")?.GetValue(raw) ?? raw.GetType().GetField("StructType")?.GetValue(raw);
				if (obj5 != null && obj5 != raw)
				{
					return (float)NumberValue(Find(obj5, name) ?? Find(obj5, fallbackName), 0.0);
				}
				return (float)NumberValue(NamedValues(raw, name).FirstOrDefault() ?? NamedValues(raw, fallbackName).FirstOrDefault(), 0.0);
			}
		}
		static float[] Vector(object? raw, float[] fallback)
		{
			if (raw != null)
			{
				return new float[3]
				{
					Component("X", fallback[0]),
					Component("Y", fallback[1]),
					Component("Z", fallback[2])
				};
			}
			return fallback;
			float Component(string name, float defaultValue)
			{
				object obj4 = raw.GetType().GetProperty(name)?.GetValue(raw) ?? raw.GetType().GetField(name)?.GetValue(raw);
				if (obj4 != null)
				{
					return (float)NumberValue(obj4, defaultValue);
				}
				object obj5 = raw.GetType().GetField("ScriptStruct")?.GetValue(raw) ?? raw.GetType().GetField("StructType")?.GetValue(raw);
				if (obj5 != null && obj5 != raw)
				{
					return ComponentFrom(obj5, name, defaultValue);
				}
				return (float)NumberValue(NamedValues(raw, name).FirstOrDefault(), defaultValue);
			}
		}
	}

	public void Inspect(string folder, IEnumerable<Cosmetic> cosmetics)
	{
		if (_provider == null)
		{
			return;
		}
		File.WriteAllText(Path.Combine(folder, "virtual-paths.json"), JsonConvert.SerializeObject(_provider.VirtualPaths, Formatting.Indented));
		File.WriteAllLines(Path.Combine(folder, "head-files.txt"), _provider.Files.Keys.Where((string p) => p.Contains("CP_Athena_Head", StringComparison.OrdinalIgnoreCase)).Take(30));
		File.WriteAllLines(Path.Combine(folder, "reference-paths.txt"), _provider.Files.Keys.Where((string p) => p.Contains("AbstractMirror_Rogue", StringComparison.OrdinalIgnoreCase) || p.Contains("BRCosmetics", StringComparison.OrdinalIgnoreCase)).Take(100));
		foreach (Cosmetic cosmetic in cosmetics)
		{
			UObject value = _provider.LoadPackageObject(cosmetic.ObjectPath);
			File.WriteAllText(Path.Combine(folder, Installation.SafeName(cosmetic.Id) + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
		}
	}

	private List<UObject> ResolveMeshes(UObject root, List<string> warnings, List<string> missing, CancellationToken ct)
	{
		List<UObject> list = new List<UObject>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Queue<(UObject, int)> queue = new Queue<(UObject, int)>();
		queue.Enqueue((root, 0));
		HashSet<string> links = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"BaseCharacterParts", "CharacterParts", "HeroDefinition", "Specializations", "CharacterPart", "SkeletalMesh", "SkeletalMeshAsset", "StaticMesh", "Mesh", "WeaponDefinition",
			"WeaponMeshOverride", "PickupSkeletalMesh", "PickupStaticMesh", "WeaponMesh", "WeaponActorClass", "ActorClass", "WeaponMeshComponent", "LeftHandWeaponMesh", "RightHandWeaponMesh", "AdditionalData",
			"DefaultCharacterParts", "BackpackMesh", "GliderMesh", "CosmeticParts", "AdditionalMeshes", "MeshOverride", "PreviewActorClass", "Outer", "OuterPart", "OuterParts",
			"OuterLayer", "OuterLayers", "OuterMesh", "OuterLayerMesh", "OuterSkeletalMesh", "OuterSkin", "AdditionalSkeletalMesh", "AdditionalSkeletalMeshes", "AdditionalParts", "LayeredMeshes",
			"MeshLayers", "PartMeshes", "CharacterPartMeshes", "Actors", "PersistentLevel", "LevelScriptActor", "WorldSettings", "Brush", "PetPrefabClass", "PetMesh",
			"ToyActorClass", "BusPrefabClass"
		};
		(UObject, int) result;
		while (queue.TryDequeue(out result))
		{
			ct.ThrowIfCancellationRequested();
			if (!hashSet.Add(result.Item1.GetPathName()))
			{
				continue;
			}
			if (result.Item1 is UClass uClass && result.Item2 < 12)
			{
				UObject uObject = uClass.ClassDefaultObject?.Load();
				if (uObject != null)
				{
					queue.Enqueue((uObject, result.Item2 + 1));
				}
				foreach (UObject item2 in from x in _provider.LoadPackage(uClass.GetPathName().Split('.')[0]).GetExports()
					where x.ExportType.Contains("MeshComponent", StringComparison.Ordinal)
					select x)
				{
					queue.Enqueue((item2, result.Item2 + 1));
				}
			}
			UObject item = result.Item1;
			bool flag = ((item is USkeletalMesh || item is UStaticMesh) ? true : false);
			if (flag && !IsPlaceholderMesh(result.Item1))
			{
				list.Add(result.Item1);
			}
			foreach (object item3 in NamedValues(result.Item1.Properties, "SkeletalMeshParameters"))
			{
				foreach (UObject item4 in from x in ReferencedObjects(item3, missing)
					where (x is USkeletalMesh || x is UStaticMesh) ? true : false
					select x)
				{
					queue.Enqueue((item4, result.Item2 + 1));
				}
			}
			if (result.Item2 >= 12 || hashSet.Count > 256)
			{
				warnings.Add("A cosmetic reference exceeded the traversal limit.");
				continue;
			}
			foreach (FPropertyTag item5 in result.Item1.Properties.Where((FPropertyTag p) => IsMeshLinkProperty(p.Name.Text, links)))
			{
				try
				{
					foreach (UObject item6 in ReferencedObjects(item5.Tag?.GenericValue, missing))
					{
						queue.Enqueue((item6, result.Item2 + 1));
					}
				}
				catch (Exception ex)
				{
					missing.Add($"{result.Item1.Name}/{item5.Name.Text}: {ex.Message}");
				}
			}
		}
		return list;
	}

	private static bool IsMeshLinkProperty(string name, HashSet<string> knownLinks)
	{
		if (!knownLinks.Contains(name))
		{
			if (name.Contains("Outer", StringComparison.OrdinalIgnoreCase) || name.Contains("Layer", StringComparison.OrdinalIgnoreCase) || name.Contains("Additional", StringComparison.OrdinalIgnoreCase) || name.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
			{
				if (!name.Contains("Mesh", StringComparison.OrdinalIgnoreCase))
				{
					return name.Contains("Part", StringComparison.OrdinalIgnoreCase);
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private IEnumerable<UObject> PartMeshes(UObject part, List<string> missing)
	{
		return (from x in part.Properties.Where((FPropertyTag p) => p.Name.Text.Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase) || p.Name.Text.Equals("Outer", StringComparison.OrdinalIgnoreCase) || p.Name.Text.Equals("OuterPart", StringComparison.OrdinalIgnoreCase) || p.Name.Text.Equals("OuterLayer", StringComparison.OrdinalIgnoreCase) || p.Name.Text.Equals("OuterLayers", StringComparison.OrdinalIgnoreCase) || IsMeshLinkProperty(p.Name.Text, new HashSet<string>(StringComparer.OrdinalIgnoreCase))).SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)).Concat(NamedValues(part.Properties, "SkeletalMeshParameters").SelectMany((object p) => ReferencedObjects(p, missing)))
			where ((x is USkeletalMesh || x is UStaticMesh) ? true : false) && !IsPlaceholderMesh(x)
			select x).DistinctBy((UObject x) => x.GetPathName());
	}

	private static bool IsPlaceholderMesh(UObject mesh)
	{
		return mesh.GetPathName().Equals("/Game/Accessories/Hats/Mesh/Hat_Empty.Hat_Empty", StringComparison.OrdinalIgnoreCase);
	}

	private Dictionary<string, UMaterialInterface> ResolveOverlayMaterials(UObject definition, string packagePath, string styleName, List<string> warnings, List<string> missing)
	{
		Dictionary<string, UMaterialInterface> dictionary = new Dictionary<string, UMaterialInterface>(StringComparer.OrdinalIgnoreCase);
		List<UObject> list = definition.Properties.Where((FPropertyTag p) =>
		{
			string text = p.Name.Text;
			return (text == "BaseCharacterParts" || text == "CharacterParts") ? true : false;
		}).SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)).ToList();
		if (!string.IsNullOrWhiteSpace(styleName) && !styleName.Equals("Default", StringComparison.OrdinalIgnoreCase))
		{
			foreach (FStructFallback item in FindStyleOptions(packagePath, styleName))
			{
				foreach (FSoftObjectPath item2 in item.Properties.Where((FPropertyTag p) =>
				{
					switch (p.Name.Text)
					{
					case "VariantParts":
					case "OuterParts":
					case "VariantOuterParts":
					case "AdditionalParts":
						return true;
					default:
						return false;
					}
				}).SelectMany((FPropertyTag p) => SoftPaths(p.Tag?.GenericValue)))
				{
					UObject uObject = ReferencedObjects(item2, missing).FirstOrDefault();
					if (uObject != null)
					{
						list.Add(uObject);
					}
				}
			}
		}
		foreach (UObject item3 in list)
		{
			UMaterialInterface uMaterialInterface = item3.Properties.Where((FPropertyTag p) => p.Name.Text.Equals("OverlayMaterialData", StringComparison.OrdinalIgnoreCase) || p.Name.Text.Contains("OuterMaterial", StringComparison.OrdinalIgnoreCase)).SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)).OfType<UMaterialInterface>()
				.FirstOrDefault();
			if (uMaterialInterface == null)
			{
				continue;
			}
			foreach (UObject item4 in PartMeshes(item3, missing))
			{
				dictionary[item4.GetPathName()] = uMaterialInterface;
			}
		}
		return dictionary;
	}

	private static IEnumerable<FSoftObjectPath> SoftPaths(object? value)
	{
		if (value is FSoftObjectPath)
		{
			yield return (FSoftObjectPath)value;
			yield break;
		}
		foreach (object item in Elements(value))
		{
			foreach (FSoftObjectPath item2 in SoftPaths(item))
			{
				yield return item2;
			}
		}
	}

	private static IEnumerable<object?> Elements(object? value)
	{
		if (value is FPropertyTag { Tag: var tag })
		{
			yield return tag?.GenericValue;
			yield break;
		}
		if (value is FPropertyTagType fPropertyTagType)
		{
			yield return fPropertyTagType.GenericValue;
			yield break;
		}
		object obj = value?.GetType().GetField("ScriptStruct")?.GetValue(value) ?? value?.GetType().GetField("StructType")?.GetValue(value);
		if (obj != null)
		{
			yield return obj;
		}
		else if (value is FStructFallback fStructFallback)
		{
			foreach (FPropertyTag property in fStructFallback.Properties)
			{
				yield return property.Tag?.GenericValue;
			}
		}
		else if (value is IEnumerable enumerable && !(value is string))
		{
			foreach (object item in enumerable)
			{
				yield return item;
			}
		}
		else
		{
			if (!((value?.GetType().GetField("Properties")?.GetValue(value) ?? value?.GetType().GetProperty("Properties")?.GetValue(value)) is IEnumerable enumerable2))
			{
				yield break;
			}
			foreach (object item2 in enumerable2)
			{
				yield return item2;
			}
		}
	}

	private Dictionary<string, List<UMaterialInterface>> ResolveMaterialOverrides(UObject definition, List<string> warnings)
	{
		Dictionary<string, List<UMaterialInterface>> dictionary = new Dictionary<string, List<UMaterialInterface>>(StringComparer.OrdinalIgnoreCase);
		foreach (FPropertyTag item in definition.Properties.Where((FPropertyTag p) => p.Name.Text == "WeaponDefinition"))
		{
			foreach (UObject item2 in ReferencedObjects(item.Tag?.GenericValue, warnings))
			{
				List<UMaterialInterface> list = NamedValues(item2.Properties, "WeaponMaterialOverrides").SelectMany((object v) => ReferencedObjects(v, warnings)).OfType<UMaterialInterface>().ToList();
				if (list.Count == 0)
				{
					continue;
				}
				foreach (FPropertyTag item3 in item2.Properties.Where((FPropertyTag p) => p.Name.Text == "WeaponMeshOverride"))
				{
					foreach (UObject item4 in ReferencedObjects(item3.Tag?.GenericValue, warnings))
					{
						dictionary[item4.GetPathName()] = list;
					}
				}
			}
		}
		return dictionary;
	}

	private void WritePickaxeFx(UObject definition, string folder, List<string> warnings)
	{
		UObject weapon = definition.Properties.Where((FPropertyTag p) => p.Name.Text == "WeaponDefinition").SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, warnings)).FirstOrDefault();
		if (weapon == null)
		{
			warnings.Add("Pickaxe FX: weapon definition could not be loaded.");
			return;
		}
		string[] array = Paths(new string[2] { "IdleEffect", "IdleEffectNiagara" });
		string[] array2 = Paths(new string[2] { "SwingEffect", "SwingEffectNiagara" });
		string[] array3 = Paths(new string[2] { "AnimTrails", "AnimTrailsNiagara" });
		string[] array4 = Paths(new string[3] { "ImpactPhysicalSurfaceEffectsMap", "ImpactNiagaraPhysicalSurfaceEffectsMap", "PickupEffectOverride" });
		var value = new
		{
			Asset = definition.Name,
			IdleEffects = array,
			SwingEffects = array2,
			Trails = array3,
			ImpactEffects = array4,
			IdleSocket = TextValue(Value("IdleFXSocketName")),
			SwingSocket = TextValue(Value("SwingFXSocketName")),
			TrailFirstSocket = TextValue(Value("AnimTrailsFirstSocketName")),
			TrailSecondSocket = TextValue(Value("AnimTrailsSecondSocketName")),
			TrailWidth = NumberValue(Value("AnimTrailsWidth"), 1.0),
			Source = weapon.GetPathName(),
			Note = "Native Cascade/Niagara playback is not available outside Unreal; Blender FX are procedural previews driven by these authored references."
		};
		if (array.Length + array2.Length + array3.Length + array4.Length == 0)
		{
			warnings.Add("Pickaxe FX: no authored effect references were found.");
		}
		File.WriteAllText(Path.Combine(folder, "fx.json"), System.Text.Json.JsonSerializer.Serialize(value, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		string[] Paths(params string[] names)
		{
			return (from path in names.SelectMany((string name) => NamedValues(weapon.Properties, name).SelectMany(SoftPaths))
				select path.AssetPathName.Text into path
				where !string.IsNullOrWhiteSpace(path)
				select path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		}
		object? Value(string name)
		{
			return NamedValues(weapon.Properties, name).FirstOrDefault();
		}
	}

	private static double NumberValue(object? value, double fallback)
	{
		try
		{
			return (value == null) ? fallback : Convert.ToDouble(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return fallback;
		}
	}

	private Dictionary<string, List<UMaterialInterface>> ResolveCharacterPartMaterialOverrides(UObject definition, List<string> warnings, IEnumerable<UObject>? selectedParts = null)
	{
		Dictionary<string, List<UMaterialInterface>> dictionary = new Dictionary<string, List<UMaterialInterface>>(StringComparer.OrdinalIgnoreCase);
		List<string> missing = new List<string>();
		foreach (UObject item in selectedParts ?? definition.Properties.Where((FPropertyTag p) =>
		{
			string text = p.Name.Text;
			return (text == "BaseCharacterParts" || text == "CharacterParts") ? true : false;
		}).SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)))
		{
			UObject[] array = PartMeshes(item, missing).ToArray();
			if (array.Length == 0)
			{
				continue;
			}
			SortedDictionary<int, UMaterialInterface> sortedDictionary = new SortedDictionary<int, UMaterialInterface>();
			foreach (FStructFallback item2 in NamedValues(item.Properties, "MaterialParameters").SelectMany((object x) => Structures(x)))
			{
				if (item2.GetOrDefault<FName>("ParameterName").Text == "ShoeMaterial")
				{
					UMaterialInterface uMaterialInterface = ReferencedObjects(item2.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "ParameterValue")?.Tag?.GenericValue, missing).OfType<UMaterialInterface>().FirstOrDefault();
					if (uMaterialInterface != null)
					{
						sortedDictionary[0] = uMaterialInterface;
					}
				}
			}
			foreach (FStructFallback item3 in Structures(item.Properties.Where((FPropertyTag p) => p.Name.Text == "MaterialOverrides")))
			{
				int key = IntValue(item3.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "MaterialOverrideIndex")?.Tag?.GenericValue);
				UMaterialInterface uMaterialInterface2 = ReferencedObjects(item3.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "OverrideMaterial")?.Tag?.GenericValue, missing).OfType<UMaterialInterface>().FirstOrDefault();
				if (uMaterialInterface2 != null)
				{
					sortedDictionary[key] = uMaterialInterface2;
				}
			}
			if (sortedDictionary.Count == 0)
			{
				continue;
			}
			UObject[] array2 = array;
			foreach (UObject uObject in array2)
			{
				foreach (KeyValuePair<int, UMaterialInterface> item4 in sortedDictionary)
				{
					string key2 = $"slot:{uObject.GetPathName()}:{item4.Key}";
					int num2 = 1;
					List<UMaterialInterface> list = new List<UMaterialInterface>(num2);
					CollectionsMarshal.SetCount(list, num2);
					CollectionsMarshal.AsSpan(list)[0] = item4.Value;
					dictionary[key2] = list;
				}
			}
		}
		foreach (string item5 in missing.Distinct())
		{
			warnings.Add("Character part reference: " + item5);
		}
		return dictionary;
	}

	private void ApplyStylePartMeshes(UObject definition, string packagePath, string styleName, List<UObject> meshes, List<string> warnings, List<string> missing, CancellationToken ct, Dictionary<string, List<UMaterialInterface>> overrides)
	{
		List<FStructFallback> list = FindStyleOptions(packagePath, styleName);
		if (list.Count == 0)
		{
			return;
		}
		PartMesh[] source = definition.Properties.Where((FPropertyTag p) =>
		{
			string text = p.Name.Text;
			return (text == "BaseCharacterParts" || text == "CharacterParts") ? true : false;
		}).SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)).ToArray()
			.SelectMany((UObject part) => from mesh3 in PartMeshes(part, missing)
				select new PartMesh(part, mesh3))
			.ToArray();
		FSoftObjectPath[] array = list.SelectMany((FStructFallback option) => option.Properties.Where((FPropertyTag p) =>
		{
			switch (p.Name.Text)
			{
			case "VariantParts":
			case "OuterParts":
			case "VariantOuterParts":
			case "AdditionalParts":
				return true;
			default:
				return false;
			}
		}).SelectMany((FPropertyTag p) => SoftPaths(p.Tag?.GenericValue))).ToArray();
		foreach (FSoftObjectPath fSoftObjectPath in array)
		{
			ct.ThrowIfCancellationRequested();
			UObject uObject = ReferencedObjects(fSoftObjectPath, missing).FirstOrDefault();
			if (uObject == null)
			{
				continue;
			}
			UObject[] array2 = PartMeshes(uObject, missing).ToArray();
			if (array2.FirstOrDefault() == null)
			{
				continue;
			}
			MergeOverrides(overrides, ResolveCharacterPartMaterialOverrides(definition, warnings, new _003C_003Ez__ReadOnlySingleElementList<UObject>(uObject)));
			string type = CharacterPartType(uObject);
			UObject[] array3;
			if (string.IsNullOrWhiteSpace(type))
			{
				warnings.Add(uObject.Name + ": style mesh loaded, but its character-part slot was not identified; the default part was retained.");
				array3 = array2;
				foreach (UObject mesh in array3)
				{
					if (!meshes.Any((UObject x) => x.GetPathName().Equals(mesh.GetPathName(), StringComparison.OrdinalIgnoreCase)))
					{
						meshes.Add(mesh);
					}
				}
				continue;
			}
			foreach (PartMesh basePart in source.Where((PartMesh x) => string.Equals(CharacterPartType(x.Part), type, StringComparison.OrdinalIgnoreCase)))
			{
				meshes.RemoveAll((UObject x) => x.GetPathName().Equals(basePart.Mesh.GetPathName(), StringComparison.OrdinalIgnoreCase));
			}
			array3 = array2;
			foreach (UObject mesh2 in array3)
			{
				if (!meshes.Any((UObject x) => x.GetPathName().Equals(mesh2.GetPathName(), StringComparison.OrdinalIgnoreCase)))
				{
					meshes.Add(mesh2);
				}
			}
		}
	}

	private string? CharacterPartType(UObject part)
	{
		string text = TextValue(part.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "CharacterPartType")?.Tag?.GenericValue);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Split("::").Last();
		}
		foreach (FPropertyTag item in part.Properties.Where((FPropertyTag p) => p.Name.Text.Contains("PortGroup", StringComparison.OrdinalIgnoreCase)))
		{
			foreach (FStructFallback item2 in Structures(item.Tag?.GenericValue))
			{
				foreach (FPropertyTag item3 in item2.Properties.Where((FPropertyTag p) => p.Name.Text.Contains("PartTypeTags", StringComparison.OrdinalIgnoreCase)))
				{
					string text2 = TextValue(item3.Tag?.GenericValue);
					if (!string.IsNullOrWhiteSpace(text2))
					{
						return text2.Split('.').Last();
					}
				}
			}
		}
		return null;
	}

	private FStructFallback? FindStyleOption(string packagePath, string styleName)
	{
		return FindStyleOptions(packagePath, styleName).FirstOrDefault();
	}

	private static void ApplyStyleToCharacterSlots(Dictionary<string, List<UMaterialInterface>> overrides, Dictionary<string, List<UMaterialInterface>> style)
	{
		foreach (KeyValuePair<string, List<UMaterialInterface>> item in style.Where((KeyValuePair<string, List<UMaterialInterface>> pair) => pair.Key.StartsWith("material:", StringComparison.OrdinalIgnoreCase)))
		{
			string b = item.Key.Substring("material:".Length);
			KeyValuePair<string, List<UMaterialInterface>>[] array = overrides.ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				KeyValuePair<string, List<UMaterialInterface>> keyValuePair = array[num];
				for (int num2 = 0; num2 < keyValuePair.Value.Count; num2++)
				{
					if (string.Equals(keyValuePair.Value[num2].GetPathName(), b, StringComparison.OrdinalIgnoreCase))
					{
						keyValuePair.Value[num2] = item.Value[0];
					}
				}
			}
		}
	}

	private Dictionary<string, List<UMaterialInterface>> ResolveStyleOverrides(UObject definition, string packagePath, string styleName, List<string> warnings)
	{
		List<FStructFallback> list = FindStyleOptions(packagePath, styleName);
		if (list.Count == 0 && !styleName.StartsWith("channels:"))
		{
			throw new IOException("The selected style was not found. Rescan this cosmetic.");
		}
		Dictionary<string, List<UMaterialInterface>> dictionary = new Dictionary<string, List<UMaterialInterface>>(StringComparer.OrdinalIgnoreCase);
		foreach (FStructFallback item in from o in list.SelectMany((FStructFallback option) => Structures(option))
			where o.Properties.Any((FPropertyTag p) => p.Name.Text == "MaterialToSwap")
			select o)
		{
			FSoftObjectPath fSoftObjectPath = SoftPaths(item.Properties.First((FPropertyTag p) => p.Name.Text == "MaterialToSwap").Tag?.GenericValue).FirstOrDefault();
			UMaterialInterface uMaterialInterface = ReferencedObjects(item.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "OverrideMaterial")?.Tag?.GenericValue, warnings).OfType<UMaterialInterface>().FirstOrDefault();
			if (uMaterialInterface == null)
			{
				throw new IOException("A style replacement material could not be loaded.");
			}
			if (!string.IsNullOrEmpty(fSoftObjectPath.AssetPathName.Text))
			{
				string key = "material:" + fSoftObjectPath.AssetPathName.Text;
				int num = 1;
				List<UMaterialInterface> list2 = new List<UMaterialInterface>(num);
				CollectionsMarshal.SetCount(list2, num);
				CollectionsMarshal.AsSpan(list2)[0] = uMaterialInterface;
				dictionary[key] = list2;
			}
		}
		return dictionary;
	}

	private static void MergeOverrides(Dictionary<string, List<UMaterialInterface>> target, Dictionary<string, List<UMaterialInterface>> source)
	{
		foreach (KeyValuePair<string, List<UMaterialInterface>> item in source)
		{
			target[item.Key] = item.Value;
		}
	}

	private static string? TextValue(object? value)
	{
		if (value == null)
		{
			return null;
		}
		if (!(value is FText fText))
		{
			return (value.GetType().GetProperty("Text") ?? value.GetType().GetProperty("SourceString") ?? value.GetType().GetProperty("LocalizedString"))?.GetValue(value)?.ToString() ?? value.ToString();
		}
		return fText.Text;
	}

	private static int IntValue(object? value)
	{
		if (value == null)
		{
			return 0;
		}
		try
		{
			return Convert.ToInt32(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			int result;
			return int.TryParse(TextValue(value), out result) ? result : 0;
		}
	}

	private static IEnumerable<object?> NamedValues(object? value, string name, int depth = 0)
	{
		if (depth > 24)
		{
			yield break;
		}
		if (value is FPropertyTag fPropertyTag && fPropertyTag.Name.Text == name)
		{
			yield return fPropertyTag.Tag?.GenericValue;
			yield break;
		}
		if (value is FStructFallback fStructFallback)
		{
			foreach (FPropertyTag property in fStructFallback.Properties)
			{
				foreach (object item in NamedValues(property, name, depth + 1))
				{
					yield return item;
				}
			}
			yield break;
		}
		foreach (object item2 in Elements(value))
		{
			foreach (object item3 in NamedValues(item2, name, depth + 1))
			{
				yield return item3;
			}
		}
	}

	private IEnumerable<UObject> ReferencedObjects(object? value, List<string> missing)
	{
		if (!(value is UObject uObject))
		{
			if (!(value is FPackageIndex fPackageIndex))
			{
				if (value is FSoftObjectPath)
				{
					FSoftObjectPath fSoftObjectPath = (FSoftObjectPath)value;
					if (!string.IsNullOrWhiteSpace(fSoftObjectPath.AssetPathName.Text) && fSoftObjectPath.AssetPathName.Text != "None")
					{
						UObject uObject2 = SafeLoad(() =>
						{
							_provider.EnsurePackage(fSoftObjectPath.AssetPathName.Text);
							return fSoftObjectPath.Load(_provider);
						}, fSoftObjectPath.AssetPathName.Text, missing);
						if (uObject2 != null)
						{
							yield return uObject2;
						}
					}
					yield break;
				}
				PropertyInfo propertyInfo = value?.GetType().GetProperty("Object") ?? value?.GetType().GetProperty("Value");
				FieldInfo fieldInfo = value?.GetType().GetField("Object") ?? value?.GetType().GetField("Value");
				object obj = null;
				try
				{
					obj = propertyInfo?.GetValue(value) ?? fieldInfo?.GetValue(value);
					if (obj != null && obj.GetType().IsGenericType && obj.GetType().GetGenericTypeDefinition() == typeof(Lazy<>))
					{
						obj = obj.GetType().GetProperty("Value")?.GetValue(obj);
					}
				}
				catch
				{
				}
				if (obj != null && obj != value)
				{
					foreach (UObject item in ReferencedObjects(obj, missing))
					{
						yield return item;
					}
				}
				foreach (object item2 in Elements(value))
				{
					foreach (UObject item3 in ReferencedObjects(item2, missing))
					{
						yield return item3;
					}
				}
			}
			else if (!fPackageIndex.IsNull)
			{
				UObject uObject3 = SafeLoad(fPackageIndex.Load, fPackageIndex.Name, missing);
				if (uObject3 != null)
				{
					yield return uObject3;
				}
			}
		}
		else
		{
			yield return uObject;
		}
	}

	private static UObject? SafeLoad(Func<UObject?> load, string name, List<string> missing)
	{
		try
		{
			UObject uObject = load();
			if (uObject == null)
			{
				missing.Add(name + ": missing reference");
			}
			return uObject;
		}
		catch (Exception ex)
		{
			missing.Add(name + ": " + ex.Message);
			return null;
		}
	}

	public void Dispose()
	{
		_provider?.Dispose();
		_provider = null;
	}

	public ExportResult AddPreviewPng(Cosmetic cosmetic, string style, ExportResult result, CancellationToken ct)
	{
		if (_provider == null)
		{
			throw new InvalidOperationException("Scan your installation first.");
		}
		_provider.EnablePackageStreaming = true;
		_provider.ExportCancellation = ct;
		List<string> list = result.Warnings.ToList();
		string category = cosmetic.Category;
		if ((category == "Terrain" || category == "Skyboxes") ? true : false)
		{
			list.Add(cosmetic.Name + ": no source icon is available for this scene.");
		}
		else
		{
			UObject definition = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
			WriteStylePreviewPng(definition, cosmetic, style, result.Folder, list, ct);
		}
		return result with
		{
			Warnings = list.Distinct().ToArray()
		};
	}

	private void WriteStylePreviewPng(UObject definition, Cosmetic cosmetic, string style, string folder, List<string> warnings, CancellationToken ct)
	{
		try
		{
			bool flag = style.StartsWith("channels:", StringComparison.Ordinal);
			FStructFallback fStructFallback = (((style == "Default") | flag) ? null : FindStyleOption(cosmetic.PackagePath ?? cosmetic.ObjectPath.Split('.')[0], style));
			UTexture2D uTexture2D = ((fStructFallback == null) ? null : NamedValues(fStructFallback, "PreviewImage").Concat(NamedValues(fStructFallback, "Icon")).SelectMany((object v) => ReferencedObjects(v, warnings)).OfType<UTexture2D>()
				.FirstOrDefault());
			bool flag2 = uTexture2D != null || style == "Default";
			if (uTexture2D == null)
			{
				uTexture2D = NamedValues(definition.Properties, "LargeIcon").Concat(NamedValues(definition.Properties, "Icon")).SelectMany((object v) => ReferencedObjects(v, warnings)).OfType<UTexture2D>()
					.FirstOrDefault();
			}
			if (!flag2)
			{
				warnings.Add(cosmetic.Name + (flag ? ": the source icon does not depict combined style choices; saved the base icon PNG." : ": no image for the selected style; saved the base icon PNG."));
			}
			if (uTexture2D == null)
			{
				warnings.Add(cosmetic.Name + ": PNG style preview was not present in the cooked definition.");
				return;
			}
			ct.ThrowIfCancellationRequested();
			byte[] array = uTexture2D.Decode()?.Encode(ETextureFormat.Png, saveHdrAsHdr: false, out string _);
			if (array == null)
			{
				warnings.Add(cosmetic.Name + ": PNG style preview could not be decoded.");
				return;
			}
			string text = Installation.SafeName(cosmetic.Name) + "_preview.png";
			File.WriteAllBytes(Path.Combine(folder, text), array);
			File.WriteAllText(Path.Combine(folder, "style-png.json"), System.Text.Json.JsonSerializer.Serialize(new
			{
				RequestedStyle = style,
				ExactStyle = flag2,
				File = text,
				Source = uTexture2D.GetPathName()
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			warnings.Add(cosmetic.Name + ": PNG style preview failed (" + ex.Message + ").");
		}
	}

	private Cosmetic LocalPreview(Cosmetic cosmetic, CancellationToken ct)
	{
		bool flag = cosmetic.ImageUrl != null;
		if (!flag)
		{
			bool flag2;
			switch (cosmetic.Category)
			{
			case "Sidekicks":
			case "Emoticons":
			case "Prefabs":
			case "Kicks":
            case "Weapons":
            case "Vehicles":
			case "Sprays":
			case "Loading screens":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		if (flag)
		{
			return cosmetic;
		}
		try
		{
			ct.ThrowIfCancellationRequested();
			string text = Path.Combine(AppSettings.DataFolder, "previews");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, Installation.SafeName(cosmetic.Id) + ".png");
			if (!File.Exists(text2))
			{
				UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
				UTexture2D uTexture2D = NamedValues(uObject.Properties, "Icon").Concat(NamedValues(uObject.Properties, "LargeIcon")).SelectMany((object v) => ReferencedObjects(v, new List<string>())).OfType<UTexture2D>()
					.FirstOrDefault();
				if (uTexture2D == null)
				{
					return cosmetic;
				}
				byte[] array = uTexture2D.Decode()?.Encode(ETextureFormat.Png, saveHdrAsHdr: false, out string _);
				if (array == null)
				{
					return cosmetic;
				}
				File.WriteAllBytes(text2, array);
			}
			return cosmetic with
			{
				ImageUrl = new Uri(text2).AbsoluteUri
			};
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			return cosmetic;
		}
	}

	private ExportResult ExportEmoticon(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend)
	{
		UObject definition = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		List<string> list = new List<string>();
		UTexture2D uTexture2D = ReferencedObjects(definition.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "SpriteSheet")?.Tag?.GenericValue, list).OfType<UTexture2D>().FirstOrDefault() ?? throw new IOException("The emoticon sprite texture could not be loaded.");
		ct.ThrowIfCancellationRequested();
		byte[] array = (uTexture2D.Decode() ?? throw new IOException("The emoticon texture could not be decoded.")).Encode(ETextureFormat.Png, saveHdrAsHdr: false, out string _) ?? throw new IOException("The emoticon texture is empty.");
		string text = Path.Combine(Path.GetFullPath(root), Installation.SafeName(cosmetic.Name) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text);
		string text2 = Installation.SafeName(cosmetic.Name);
		File.WriteAllBytes(Path.Combine(text, text2 + ".png"), array);
		int num = (from v in new string[5] { "FrameCount", "NumFrames", "Frames", "SpriteFrames", "AnimationFrameCount" }.SelectMany((string name) => NamedValues(definition.Properties, name))
			select IntValue(v)).FirstOrDefault((int v) => v > 1);
		double num2 = (from v in new string[4] { "FramesPerSecond", "FrameRate", "FPS", "PlaybackRate" }.SelectMany((string name) => NamedValues(definition.Properties, name))
			select NumberValue(v, 0.0)).FirstOrDefault((double v) => v > 0.0);
		int num3 = (from v in new string[3] { "Columns", "NumColumns", "SpriteSheetColumns" }.SelectMany((string name) => NamedValues(definition.Properties, name))
			select IntValue(v)).FirstOrDefault((int v) => v > 0);
		int num4 = (from v in new string[3] { "Rows", "NumRows", "SpriteSheetRows" }.SelectMany((string name) => NamedValues(definition.Properties, name))
			select IntValue(v)).FirstOrDefault((int v) => v > 0);
		object dimensions = NamedValues(definition.Properties, "SheetDimensions").FirstOrDefault();
		if (dimensions != null)
		{
			num3 = Math.Max(num3, IntValue(Component("X")));
			num4 = Math.Max(num4, IntValue(Component("Y")));
		}
		List<string> list2 = new List<string>();
		if (num > 1)
		{
			using SKBitmap sKBitmap = SKBitmap.Decode(array) ?? throw new IOException("The emoticon sprite sheet could not be read.");
			if (num3 <= 0 || num4 <= 0)
			{
				num3 = ((sKBitmap.Width >= sKBitmap.Height * num) ? num : ((int)Math.Ceiling(Math.Sqrt(num))));
				num4 = (int)Math.Ceiling((double)num / (double)num3);
			}
			int num5 = Math.Max(1, sKBitmap.Width / num3);
			int num6 = Math.Max(1, sKBitmap.Height / num4);
			string text3 = Path.Combine(text, "frames");
			Directory.CreateDirectory(text3);
			for (int num7 = 0; num7 < num; num7++)
			{
				int num8 = num7 % num3 * num5;
				int num9 = num7 / num3 * num6;
				if (num8 >= sKBitmap.Width || num9 >= sKBitmap.Height)
				{
					break;
				}
				int num10 = Math.Min(num5, sKBitmap.Width - num8);
				int num11 = Math.Min(num6, sKBitmap.Height - num9);
				using SKBitmap bitmap = new SKBitmap(num10, num11);
				using (SKCanvas sKCanvas = new SKCanvas(bitmap))
				{
					sKCanvas.Clear(SKColors.Transparent);
					sKCanvas.DrawBitmap(sKBitmap, new SKRect(num8, num9, num8 + num10, num9 + num11), new SKRect(0f, 0f, num10, num11));
				}
				using SKImage sKImage = SKImage.FromBitmap(bitmap);
				using SKData sKData = sKImage.Encode(SKEncodedImageFormat.Png, 100);
				string text4 = $"frame_{num7 + 1:0000}.png";
				File.WriteAllBytes(Path.Combine(text3, text4), sKData.ToArray());
				list2.Add("frames/" + text4);
			}
		}
		if (list2.Count == 0)
		{
			list2.Add(text2 + ".png");
		}
		bool animated = list2.Count > 1;
		if (num2 <= 0.0)
		{
			num2 = 12.0;
		}
		var value = new
		{
			Blend = text2 + ".blend",
			Emoticon = text2 + ".png",
			Animated = animated,
			Frames = list2.Count,
			Fps = num2,
			Columns = num3,
			Rows = num4,
			FrameFiles = list2,
			Materials = Array.Empty<object>()
		};
		File.WriteAllText(Path.Combine(text, "materials.json"), System.Text.Json.JsonSerializer.Serialize(value));
		File.WriteAllText(Path.Combine(text, "emoticon.json"), JsonConvert.SerializeObject(new
		{
			Cosmetic = cosmetic.Name,
			SpriteSheet = uTexture2D.GetPathName(),
			Animated = animated,
			Frames = list2.Count,
			Fps = num2,
			Columns = num3,
			Rows = num4,
			FrameFiles = list2,
			Definition = definition
		}, Formatting.Indented));
		try
		{
			BlenderExport.Create(text, list, progress, ct);
			File.WriteAllText(Path.Combine(text, "export-report.json"), System.Text.Json.JsonSerializer.Serialize(new
			{
				Name = cosmetic.Name,
				Texture = uTexture2D.GetPathName(),
				Warnings = list
			}));
			if (onlyBlend)
			{
				CleanIntermediates(text);
			}
			return new ExportResult(text, 1, 1, list.ToArray());
		}
		catch
		{
			File.WriteAllText(Path.Combine(text, "INCOMPLETE.txt"), "Emoticon conversion failed. The source PNG was retained.");
			throw;
		}
		object? Component(string name)
		{
			object? obj2 = dimensions.GetType().GetField(name)?.GetValue(dimensions);
			if (obj2 == null)
			{
				PropertyInfo? property = dimensions.GetType().GetProperty(name);
				if ((object)property == null)
				{
					return null;
				}
				obj2 = property.GetValue(dimensions);
			}
			return obj2;
		}
	}

	private ExportResult ExportImageCosmetic(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend)
	{
		UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		List<string> warnings = new List<string>();
		string text = ((cosmetic.Category == "Sprays") ? "DecalTexture" : "BackgroundImage");
		UTexture2D? texture = NamedValues(uObject.Properties, text).SelectMany((object x) => ReferencedObjects(x, warnings)).OfType<UTexture2D>().FirstOrDefault() ?? throw new IOException(cosmetic.Name + ": the source " + text + " could not be loaded.");
		ct.ThrowIfCancellationRequested();
		progress.Report("Decoding " + cosmetic.Name + "…");
		byte[] bytes = texture.Decode()?.Encode(ETextureFormat.Png, saveHdrAsHdr: false, out string _) ?? throw new IOException(cosmetic.Name + ": the source image could not be decoded.");
		string text2 = Installation.SafeName(cosmetic.Name);
		string text3 = Path.Combine(Path.GetFullPath(root), text2 + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text3);
		try
		{
			File.WriteAllBytes(Path.Combine(text3, text2 + ".png"), bytes);
			File.WriteAllText(Path.Combine(text3, "materials.json"), System.Text.Json.JsonSerializer.Serialize(new
			{
				Emoticon = text2 + ".png",
				ImageAssetName = cosmetic.Name,
				Blend = text2 + ".blend",
				Frames = 1,
				Columns = 1,
				Rows = 1,
				Materials = Array.Empty<object>()
			}));
			BlenderExport.Create(text3, warnings, progress, ct, required: true);
			if (onlyBlend)
			{
				CleanIntermediates(text3);
			}
			return new ExportResult(text3, 1, 1, warnings.ToArray());
		}
		catch
		{
			File.WriteAllText(Path.Combine(text3, "INCOMPLETE.txt"), "Image export did not finish. Retry before using these files.");
			throw;
		}
	}

	private List<PrefabPlacement>? SavedPrefabPlacements(UObject definition, List<string> warnings, CancellationToken ct)
	{
		FPropertyTag fPropertyTag = definition.Properties.FirstOrDefault((FPropertyTag x) => x.Name.Text == "PlaysetPropLevelSaveRecordCollection");
		if (fPropertyTag == null)
		{
			return null;
		}
		UObject uObject = ReferencedObjects(fPropertyTag.Tag?.GenericValue, warnings).FirstOrDefault();
		if (uObject == null)
		{
			throw new IOException("The prefab's saved placement collection could not be read.");
		}
		FStructFallback[] array = (from s in Structures(uObject.Properties.Where((FPropertyTag p) => p.Name.Text == "Items"))
			where s.Properties.Any((FPropertyTag p) => p.Name.Text == "LevelSaveRecord")
			select s).ToArray();
		if (array.Length == 0)
		{
			return null;
		}
		Dictionary<string, (string, StyleMaterialParameters, UMaterialInterface)> dictionary = new Dictionary<string, (string, StyleMaterialParameters, UMaterialInterface)>();
		List<PrefabPlacement> list = new List<PrefabPlacement>();
		FStructFallback[] array2 = array;
		foreach (FStructFallback fStructFallback in array2)
		{
			ct.ThrowIfCancellationRequested();
			FSoftObjectPath path = fStructFallback.GetOrDefault<FSoftObjectPath>("LevelSaveRecord");
			if (!dictionary.TryGetValue(path.AssetPathName.Text, out var value))
			{
				_provider.EnsurePackage(path.AssetPathName.Text);
				ULevelSaveRecord? uLevelSaveRecord = _provider.LoadPackage(path.AssetPathName.Text.Split('.')[0]).GetExports().OfType<ULevelSaveRecord>()
					.ToArray()
					.SingleOrDefault((ULevelSaveRecord x) => string.IsNullOrEmpty(path.SubPathString) || x.Name == path.SubPathString) ?? throw new IOException("Saved prefab record is missing: " + path.AssetPathName.Text);
				if (uLevelSaveRecord.TemplateRecords.Count != 1)
				{
					throw new IOException("This prefab prop uses multiple actor templates: " + path.AssetPathName.Text);
				}
				FActorTemplateRecord fActorTemplateRecord = uLevelSaveRecord.TemplateRecords.Values.Single();
				StyleMaterialParameters styleMaterialParameters = new StyleMaterialParameters();
				UMaterialInterface item = null;
				foreach (var (num2, num3) in ReadTextureDataReferences(fActorTemplateRecord.ActorData ?? Array.Empty<byte>()))
				{
					if (num3 >= 0)
					{
						if (!fActorTemplateRecord.bUsingRecordDataReferenceTable || num3 >= fActorTemplateRecord.ActorDataReferenceTable.Length)
						{
							throw new IOException("Invalid saved texture-data reference in " + path.AssetPathName.Text);
						}
						string text = fActorTemplateRecord.ActorDataReferenceTable[num3].AssetPathName.Text;
						_provider.EnsurePackage(text);
						UBuildingTextureData uBuildingTextureData = _provider.LoadPackageObject<UBuildingTextureData>(text);
						string text2 = ((num2 == 0) ? "" : ("_" + (num2 + 1)));
						UTexture uTexture = uBuildingTextureData.Diffuse.Load<UTexture>();
						if (uTexture != null)
						{
							styleMaterialParameters.Textures[(num2 == 0) ? "Diffuse" : ("Diffuse_Texture" + text2)] = uTexture;
						}
						UTexture uTexture2 = uBuildingTextureData.Normal.Load<UTexture>();
						if (uTexture2 != null)
						{
							styleMaterialParameters.Textures[(num2 == 0) ? "Normals" : ("Normals_Texture" + text2)] = uTexture2;
						}
						UTexture uTexture3 = uBuildingTextureData.Specular.Load<UTexture>();
						if (uTexture3 != null)
						{
							styleMaterialParameters.Textures["SpecularMasks" + text2] = uTexture3;
						}
						UMaterialInterface uMaterialInterface = uBuildingTextureData.OverrideMaterial.Load<UMaterialInterface>();
						if (uMaterialInterface != null)
						{
							item = uMaterialInterface;
						}
						FColor? tintColor = uBuildingTextureData.TintColor;
						if (tintColor.HasValue)
						{
							FColor valueOrDefault = tintColor.GetValueOrDefault();
							styleMaterialParameters.Colors["Base Color Multiplier" + text2] = new FLinearColor((float)(int)valueOrDefault.R / 255f, (float)(int)valueOrDefault.G / 255f, (float)(int)valueOrDefault.B / 255f, (float)(int)valueOrDefault.A / 255f);
						}
					}
				}
				value = (fActorTemplateRecord.ActorClass.AssetPathName.Text, styleMaterialParameters, item);
				dictionary[path.AssetPathName.Text] = value;
			}
			list.Add(new PrefabPlacement(value.Item1, fStructFallback.GetOrDefault("Transform", FTransform.Identity), value.Item2, value.Item3));
		}
		return list;
	}

	internal static IEnumerable<(int Slot, int Reference)> ReadTextureDataReferences(byte[] bytes)
	{
		using MemoryStream signature = new MemoryStream();
		using (BinaryWriter binaryWriter = new BinaryWriter(signature, Encoding.UTF8, leaveOpen: true))
		{
			string[] array = new string[2] { "TextureData", "ObjectProperty" };
			foreach (string text in array)
			{
				binaryWriter.Write(text.Length + 1);
				binaryWriter.Write(Encoding.UTF8.GetBytes(text + "\0"));
			}
			binaryWriter.Write(4);
		}
		byte[] prefix = signature.ToArray();
		for (int j = 0; j + prefix.Length + 9 <= bytes.Length; j++)
		{
			if (((ReadOnlySpan<byte>)bytes.AsSpan(j, prefix.Length)).SequenceEqual((ReadOnlySpan<byte>)prefix))
			{
				int cursor = j + prefix.Length;
				int num = BitConverter.ToInt32(bytes, cursor);
				cursor += 4;
				if (num < 0 || num > 5)
				{
					throw new IOException("Unsupported prefab texture-data slot.");
				}
				byte b = bytes[cursor++];
				if (b > 1)
				{
					throw new IOException("Invalid saved texture-data tag.");
				}
				if (b == 1)
				{
					cursor += 16;
				}
				if (cursor + 4 > bytes.Length)
				{
					throw new IOException("Truncated saved texture-data reference.");
				}
				yield return (Slot: num, Reference: BitConverter.ToInt32(bytes, cursor));
				j = cursor + 3;
			}
		}
	}

	private ExportResult ExportSkybox(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend)
	{
		ct.ThrowIfCancellationRequested();
		string text = Path.Combine(Path.GetFullPath(root), Installation.SafeName(cosmetic.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
		Directory.CreateDirectory(text);
		Cosmetic? cosmetic2 = BuiltInSkyboxes.FirstOrDefault((Cosmetic x) => x.Id == cosmetic.Id);
		string text2 = (((object)cosmetic2 != null) ? cosmetic2.Name.Split(' ')[0] : null) ?? "Daylight";
		File.WriteAllText(Path.Combine(text, "materials.json"), JsonConvert.SerializeObject(new
		{
			Skybox = true,
			SkyPreset = text2,
			Blend = "Skybox_" + text2 + ".blend",
			Materials = Array.Empty<object>()
		}, Formatting.Indented));
		progress.Report("Creating " + text2.ToLowerInvariant() + " skybox…");
		File.WriteAllText(Path.Combine(text, "skybox.json"), JsonConvert.SerializeObject(new
		{
			Skybox = true,
			SkyPreset = text2,
			Name = cosmetic.Name
		}, Formatting.Indented));
		List<string> list = new List<string>();
		BlenderExport.Create(text, list, progress, ct, required: true);
		if (onlyBlend)
		{
			CleanIntermediates(text);
		}
		return new ExportResult(text, 0, 0, list.ToArray());
	}

	private static FPackageIndex[]? ActorMaterials(UObject? actor)
	{
		HashSet<string> hashSet = new HashSet<string>();
		while (actor != null && hashSet.Add(actor.GetPathName()) && hashSet.Count < 40)
		{
			string[] array = new string[3] { "OverrideMaterials", "StaticMeshMaterials", "MaterialOverrides" };
			foreach (string name in array)
			{
				FPackageIndex[] orDefault = actor.GetOrDefault<FPackageIndex[]>(name);
				if (orDefault != null && orDefault.Length > 0)
				{
					return orDefault;
				}
			}
			if (!(actor.Class?.Load() is UClass { ClassDefaultObject: var classDefaultObject } uClass))
			{
				break;
			}
			actor = classDefaultObject?.Load();
			if (actor != null && hashSet.Contains(actor.GetPathName()))
			{
				actor = (uClass.SuperStruct?.Load() as UClass)?.ClassDefaultObject?.Load();
			}
		}
		return null;
	}

	public static RigSceneExporter.SceneTransform SceneTransform(FTransform t)
	{
		return new RigSceneExporter.SceneTransform(new float[3]
		{
			t.Translation.X,
			t.Translation.Y,
			t.Translation.Z
		}, new float[3], new float[3]
		{
			t.Scale3D.X,
			t.Scale3D.Y,
			t.Scale3D.Z
		}, new float[4]
		{
			t.Rotation.X,
			t.Rotation.Y,
			t.Rotation.Z,
			t.Rotation.W
		});
	}

	private static FTransform ComponentWorld(USceneComponent component)
	{
		FTransform relativeTransform = component.GetRelativeTransform();
		HashSet<string> hashSet = new HashSet<string> { component.GetPathName() };
		for (USceneComponent attachParent = component.GetAttachParent(); attachParent != null; attachParent = attachParent.GetAttachParent())
		{
			if (!hashSet.Add(attachParent.GetPathName()) || hashSet.Count > 128)
			{
				throw new IOException("Invalid component attachment hierarchy.");
			}
			relativeTransform *= attachParent.GetRelativeTransform();
		}
		return relativeTransform;
	}

	private static UStaticMesh? ActorMesh(UObject? actor)
	{
		HashSet<string> hashSet = new HashSet<string>();
		while (actor != null && hashSet.Add(actor.GetPathName()) && hashSet.Count < 40)
		{
			UStaticMesh uStaticMesh = actor.GetOrDefault<FPackageIndex>("StaticMesh")?.Load<UStaticMesh>();
			if (uStaticMesh != null)
			{
				return uStaticMesh;
			}
			if (!(actor.Class?.Load() is UClass { ClassDefaultObject: var classDefaultObject } uClass))
			{
				break;
			}
			actor = classDefaultObject?.Load();
			if (actor != null && hashSet.Contains(actor.GetPathName()))
			{
				actor = (uClass.SuperStruct?.Load() as UClass)?.ClassDefaultObject?.Load();
			}
		}
		return null;
	}

	private List<MapMeshInstance> PlacedComponents(IEnumerable<UObject> exports, List<string> warnings, CancellationToken ct)
	{
		List<MapMeshInstance> list = new List<MapMeshInstance>();
		foreach (UObject export in exports)
		{
			ct.ThrowIfCancellationRequested();
			try
			{
				if (export.GetOrDefault("bIsEditorOnly", defaultValue: false) || export.GetOrDefault("bHiddenInGame", defaultValue: false) || !export.GetOrDefault("bVisible", defaultValue: true))
				{
					continue;
				}
				if (export is ALandscapeProxy aLandscapeProxy)
				{
					USceneComponent uSceneComponent = aLandscapeProxy.GetOrDefault<FPackageIndex>("RootComponent")?.Load<USceneComponent>();
					if (uSceneComponent == null)
					{
						warnings.Add(aLandscapeProxy.Name + ": landscape root transform missing.");
					}
					else
					{
						list.Add(new MapMeshInstance(aLandscapeProxy, SceneTransform(ComponentWorld(uSceneComponent))));
					}
				}
				else
				{
					if (!(export is UStaticMeshComponent uStaticMeshComponent))
					{
						continue;
					}
					UStaticMesh uStaticMesh = uStaticMeshComponent.GetLoadedStaticMesh() ?? ActorMesh(uStaticMeshComponent.Outer?.Load());
					if (uStaticMesh == null || uStaticMesh.Name.Contains("SplineEditorMesh", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					FTransform fTransform = ComponentWorld(uStaticMeshComponent);
					if (uStaticMeshComponent is UInstancedStaticMeshComponent uInstancedStaticMeshComponent)
					{
						FInstancedStaticMeshInstanceData[] instances = uInstancedStaticMeshComponent.GetInstances();
						foreach (FInstancedStaticMeshInstanceData fInstancedStaticMeshInstanceData in instances)
						{
							list.Add(new MapMeshInstance(uStaticMesh, SceneTransform(fInstancedStaticMeshInstanceData.TransformData * fTransform), uStaticMeshComponent));
						}
					}
					else
					{
						list.Add(new MapMeshInstance(uStaticMesh, SceneTransform(fTransform), uStaticMeshComponent));
					}
					continue;
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				warnings.Add(export.Name + ": " + ex.Message);
			}
		}
		return list;
	}

	private ExportResult ExportMap(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend, bool matteMaterials)
	{
		List<string> list = new List<string>();
		UObject[] array = _provider.LoadPackage(cosmetic.ObjectPath).GetExports().ToArray();
		List<MapMeshInstance> instances = PlacedComponents(array, list, ct);
		if (array.Any((UObject x) => x.ExportType.Contains("LevelStreaming", StringComparison.OrdinalIgnoreCase)))
		{
			list.Add("This export contains the selected level. Referenced streaming levels are not assembled automatically.");
		}
		return ExportInstances(cosmetic, root, instances, list, progress, ct, onlyBlend, matteMaterials);
	}

	private ExportResult ExportPrefab(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend, bool matteMaterials)
	{
		UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		List<string> list = new List<string>();
		List<PrefabPlacement>? list2 = SavedPrefabPlacements(uObject, list, ct);
		List<PrefabPlacement> list3 = list2 ?? (from r in Structures(uObject.Properties.Where((FPropertyTag p) => p.Name.Text == "PreviewActorData"))
			where r.Properties.Any((FPropertyTag p) => p.Name.Text == "ActorClass")
			select new PrefabPlacement(r.GetOrDefault<FSoftObjectPath>("ActorClass").AssetPathName.Text, new FTransform(r.GetOrDefault("RelativeRotation", FRotator.ZeroRotator).Quaternion(), r.GetOrDefault("RelativeLocation", FVector.ZeroVector), r.GetOrDefault("RelativeScale3D", FVector.OneVector)))).ToList();
		if (list3.Count == 0)
		{
			throw new IOException("This prefab has no readable actor placements; no empty scene was exported.");
		}
		List<MapMeshInstance> list4 = new List<MapMeshInstance>();
		if (list2 == null)
		{
			list.Add("Saved prefab placements are unavailable; preview placements and default materials were used.");
		}
		Dictionary<string, List<MapMeshInstance>> dictionary = new Dictionary<string, List<MapMeshInstance>>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (PrefabPlacement item in list3)
		{
			ct.ThrowIfCancellationRequested();
			string actorClass = item.ActorClass;
			try
			{
				if (!dictionary.TryGetValue(actorClass, out var value))
				{
					_provider.EnsurePackage(actorClass);
					UObject actor = _provider.LoadPackageObject<UClass>(actorClass).ClassDefaultObject?.Load();
					UStaticMesh uStaticMesh = ActorMesh(actor);
					if (uStaticMesh != null)
					{
						int num2 = 1;
						List<MapMeshInstance> list5 = new List<MapMeshInstance>(num2);
						CollectionsMarshal.SetCount(list5, num2);
						CollectionsMarshal.AsSpan(list5)[0] = new MapMeshInstance(uStaticMesh, SceneTransform(FTransform.Identity), null, ActorMaterials(actor));
						value = list5;
					}
					else
					{
						value = PlacedComponents(_provider.LoadPackage(actorClass.Split('.')[0]).GetExports(), list, ct);
					}
					dictionary[actorClass] = value;
				}
				if (value.Count == 0)
				{
					num++;
					continue;
				}
				FTransform transform = item.Transform;
				foreach (MapMeshInstance item2 in value)
				{
					RigSceneExporter.SceneTransform transform2 = item2.Transform;
					float[] quaternion = transform2.Quaternion;
					FTransform fTransform = new FTransform(new FQuat(quaternion[0], quaternion[1], quaternion[2], quaternion[3]), new FVector(transform2.Location[0], transform2.Location[1], transform2.Location[2]), new FVector(transform2.Scale[0], transform2.Scale[1], transform2.Scale[2]));
					list4.Add(item2 with
					{
						Transform = SceneTransform(fTransform * transform),
						Parameters = item.Parameters,
						SavedMaterial = item.Material
					});
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				num++;
				list.Add(actorClass + ": " + ex.Message);
			}
		}
		if (num > 0)
		{
			throw new IOException($"{num} of {list3.Count} prefab actors could not be loaded. The prefab was not exported incomplete. " + string.Join("\n", list.Take(3)));
		}
		return ExportInstances(cosmetic, root, list4, list, progress, ct, onlyBlend, matteMaterials);
	}

	private ExportResult ExportInstances(Cosmetic cosmetic, string root, List<MapMeshInstance> instances, List<string> warnings, IProgress<string> progress, CancellationToken ct, bool onlyBlend, bool matteMaterials)
	{
		if (instances.Count == 0)
		{
			throw new IOException("No placed geometry could be decoded in this level. This may be a streaming container or an unsupported level format. No scene was exported.");
		}
		string text = Path.Combine(Path.GetFullPath(root), Installation.SafeName(cosmetic.Name) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text);
		try
		{
			float[] origin = new float[3]
			{
				instances.Average((MapMeshInstance x) => x.Transform.Location[0]),
				instances.Average((MapMeshInstance x) => x.Transform.Location[1]),
				instances.Average((MapMeshInstance x) => x.Transform.Location[2])
			};
			MapMeshInstance[] source = instances.ToArray();
			instances = instances.Select((MapMeshInstance x) => x with
			{
				Transform = x.Transform with
				{
					Location = x.Transform.Location.Select((float v, int i) => v - origin[i]).ToArray()
				}
			}).ToList();
			File.WriteAllText(Path.Combine(text, "map.json"), JsonConvert.SerializeObject(new
			{
				Name = cosmetic.Name,
				ObjectPath = cosmetic.ObjectPath,
				WorldOrigin = origin,
				MeshCount = instances.Count,
				Meshes = source.Select((MapMeshInstance x) => new
				{
					x.Mesh.Name,
					x.Transform
				})
			}, Formatting.Indented));
			Dictionary<string, List<UMaterialInterface>> dictionary = new Dictionary<string, List<UMaterialInterface>>();
			Dictionary<string, StyleMaterialParameters> dictionary2 = new Dictionary<string, StyleMaterialParameters>();
			for (int num = 0; num < instances.Count; num++)
			{
				FPackageIndex[] array = instances[num].Component?.GetOrDefault<FPackageIndex[]>("OverrideMaterials") ?? instances[num].Materials;
				if (array != null && array.Length > 0)
				{
					dictionary["instance:" + num] = array.Select((FPackageIndex s) => s.Load<UMaterialInterface>()).ToList();
				}
				UMaterialInterface savedMaterial = instances[num].SavedMaterial;
				if (savedMaterial != null)
				{
					string key = "instance:" + num;
					int num2 = 1;
					List<UMaterialInterface> list = new List<UMaterialInterface>(num2);
					CollectionsMarshal.SetCount(list, num2);
					CollectionsMarshal.AsSpan(list)[0] = savedMaterial;
					dictionary[key] = list;
				}
				StyleMaterialParameters parameters = instances[num].Parameters;
				if (parameters != null)
				{
					dictionary2["instance:" + num] = parameters;
				}
			}
			progress.Report($"Exporting {instances.Count:N0} placed meshes…");
			return ObjExporter.Export(instances.Select((MapMeshInstance x) => x.Mesh).ToList(), cosmetic, text, warnings, progress, ct, dictionary, onlyBlend, matteMaterials, instances.Select((MapMeshInstance x) => x.Transform).ToArray(), instances.Select((MapMeshInstance x) => x.Component as USplineMeshComponent).ToArray(), dictionary2);
		}
		catch
		{
			File.WriteAllText(Path.Combine(text, "INCOMPLETE.txt"), "Scene export did not finish. See the session log.");
			throw;
		}
	}

	private ExportResult ExportWrap(Cosmetic cosmetic, string root, IProgress<string> progress, CancellationToken ct, bool onlyBlend, bool matteMaterials)
	{
		UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		List<string> warnings = new List<string>();
		UMaterialInterface uMaterialInterface = NamedValues(uObject.Properties, "ItemWrapMaterial").SelectMany((object x) => ReferencedObjects(x, warnings)).OfType<UMaterialInterface>().FirstOrDefault() ?? throw new IOException("This wrap's material could not be loaded.");
		string text = Installation.SafeName(cosmetic.Name);
		string text2 = Path.Combine(Path.GetFullPath(root), text + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		Directory.CreateDirectory(text2);
		try
		{
			progress.Report("Exporting wrap material: " + cosmetic.Name);
			MaterialExporter materialExporter = new MaterialExporter(text2, warnings, ct, matteMaterials);
			using (StreamWriter streamWriter = new StreamWriter(Path.Combine(text2, text + ".mtl")))
			{
				streamWriter.WriteLine("newmtl Wrap");
				materialExporter.Write(streamWriter, "Wrap", uMaterialInterface);
			}
			File.WriteAllText(Path.Combine(text2, "materials.json"), System.Text.Json.JsonSerializer.Serialize(new
			{
				WrapPreview = true,
				Blend = text + ".blend",
				Materials = materialExporter.Materials
			}));
			File.WriteAllText(Path.Combine(text2, "wrap.json"), System.Text.Json.JsonSerializer.Serialize(new
			{
				Name = cosmetic.Name,
				Source = uMaterialInterface.GetPathName(),
				Preview = "Generated sphere; append the Wrap material to apply it to your own mesh."
			}));
			BlenderExport.Create(text2, warnings, progress, ct, required: true);
			if (onlyBlend)
			{
				CleanIntermediates(text2);
			}
			return new ExportResult(text2, 1, materialExporter.Textures.Count(), warnings.ToArray());
		}
		catch
		{
			File.WriteAllText(Path.Combine(text2, "INCOMPLETE.txt"), "Wrap export did not finish.");
			throw;
		}
	}

	public void InspectCharacterParts(Cosmetic cosmetic, string folder)
	{
		UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		List<string> missing = new List<string>();
		UObject[] value = uObject.Properties.Where((FPropertyTag p) => p.Name.Text == "BaseCharacterParts").SelectMany((FPropertyTag p) => ReferencedObjects(p.Tag?.GenericValue, missing)).ToArray();
		File.WriteAllText(Path.Combine(folder, "character-parts.json"), JsonConvert.SerializeObject(value, Formatting.Indented));
        File.WriteAllText(Path.Combine(folder, "character-part-data.json"), JsonConvert.SerializeObject(value.SelectMany(part => _provider.LoadPackage(part.GetPathName().Split('.')[0]).GetExports()).Distinct(), Formatting.Indented));
	}

	public void InspectDefinitions(string folder, IEnumerable<Cosmetic> items)
	{
		foreach (Cosmetic item in items)
		{
			string path = ((item.Category == "Terrain") ? item.ObjectPath : (item.PackagePath ?? item.ObjectPath).Split('.')[0]);
			IEnumerable<UObject> exports = _provider.LoadPackage(path).GetExports();
			File.WriteAllText(Path.Combine(folder, Installation.SafeName(item.Name) + "-definition.json"), JsonConvert.SerializeObject(exports, Formatting.Indented));
		}
	}

	public void InspectQuality(Cosmetic cosmetic, string folder)
	{
		_provider.EnablePackageStreaming = true;
		List<string> warnings = new List<string>();
		List<string> missing = new List<string>();
		UObject uObject = _provider.LoadPackageObject(cosmetic.PackagePath ?? cosmetic.ObjectPath);
		string[] source = (from p in SoftPaths(uObject.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "BaseCharacterParts")?.Tag?.GenericValue)
			select p.AssetPathName.Text).ToArray();
		File.WriteAllText(Path.Combine(folder, "part-locations.json"), System.Text.Json.JsonSerializer.Serialize(source.Select((string path) => new
		{
			Path = path,
			Local = _provider.TryGetGameFile(path.Split('.')[0], out GameFile _),
			Stream = _provider.StreamContainer(path),
			Matches = _provider.Files.Keys.Where((string k) => k.Contains(path.Split('.').Last(), StringComparison.OrdinalIgnoreCase)).ToArray()
		}), new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		File.WriteAllText(Path.Combine(folder, "cosmetic-definition.json"), JsonConvert.SerializeObject(uObject, Formatting.Indented));
		File.WriteAllText(Path.Combine(folder, "cosmetic-exports.json"), JsonConvert.SerializeObject(_provider.LoadPackage((cosmetic.PackagePath ?? cosmetic.ObjectPath).Split('.')[0]).GetExports(), Formatting.Indented));
		foreach (FPropertyTag item in uObject.Properties.Where((FPropertyTag p) => p.Name.Text == "WeaponDefinition"))
		{
			foreach (UObject item2 in ReferencedObjects(item.Tag?.GenericValue, missing))
			{
				File.WriteAllText(Path.Combine(folder, "weapon-definition.json"), JsonConvert.SerializeObject(item2, Formatting.Indented));
				foreach (FPropertyTag item3 in item2.Properties.Where((FPropertyTag p) => p.Name.Text == "WeaponActorClass"))
				{
					foreach (UObject item4 in ReferencedObjects(item3.Tag?.GenericValue, missing))
					{
						File.WriteAllText(Path.Combine(folder, "actor-class.json"), JsonConvert.SerializeObject(item4, Formatting.Indented));
						UObject uObject2 = (item4 as UClass)?.ClassDefaultObject.Load();
						if (uObject2 != null)
						{
							File.WriteAllText(Path.Combine(folder, "actor-defaults.json"), JsonConvert.SerializeObject(uObject2, Formatting.Indented));
						}
					}
				}
			}
		}
		List<UObject> list = ResolveMeshes(uObject, warnings, missing, CancellationToken.None);
		Dictionary<string, List<UMaterialInterface>> overrides = ResolveMaterialOverrides(uObject, warnings);
		List<object> details = new List<object>();
		foreach (UObject item5 in list)
		{
			if (item5 is USkeletalMesh mesh)
			{
				using SkeletalMeshDto dto = new SkeletalMeshDto(mesh, EMeshQuality.All, ENaniteMeshFormat.NaniteLast, exportMorphTarget: false);
				Inspect<SkinnedMeshVertex>(dto, item5);
			}
			else if (item5 is UStaticMesh mesh2)
			{
				using StaticMeshDto dto2 = new StaticMeshDto(mesh2, EMeshQuality.All, ENaniteMeshFormat.NaniteLast);
				Inspect<MeshVertex>(dto2, item5);
			}
		}
		File.WriteAllText(Path.Combine(folder, "source-quality.json"), System.Text.Json.JsonSerializer.Serialize(details, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		object Chunks(UTexture t)
		{
			if (_provider.TryGetGameFile(t.Owner.Name, out GameFile file))
			{
				FIoStoreEntry entry = file as FIoStoreEntry;
				if (entry != null)
				{
					return _provider.MountedVfs.OfType<IoStoreReader>().SelectMany((IoStoreReader r) => from c in r.TocResource.ChunkIds
						where c.ChunkId == entry.ChunkId.ChunkId
						select new
						{
							Container = r.Name,
							Id = c.ToString(),
							Type = ((EIoChunkType5)c.ChunkType/*cast due to constrained. prefix*/).ToString()
						}).ToArray();
				}
			}
			return "Unknown owner";
		}
		void Inspect<T>(MeshDto<T> meshDto, UObject uObject3) where T : struct, IMeshVertex
		{
			List<object> list2 = new List<object>();
			for (int i = 0; i < meshDto.Materials.Length; i++)
			{
				List<UMaterialInterface> valueOrDefault = overrides.GetValueOrDefault(uObject3.GetPathName());
				if (((valueOrDefault != null && i < valueOrDefault.Count) ? valueOrDefault[i] : meshDto.Materials[i].Material?.Load()) is UMaterialInterface uMaterialInterface)
				{
					CMaterialParams2 cMaterialParams = new CMaterialParams2();
					uMaterialInterface.GetParams(cMaterialParams, EMaterialDepth.AllLayers);
					List<object> list3 = new List<object>();
					UUnrealMaterial uUnrealMaterial = uMaterialInterface;
					int num = 0;
					while (uUnrealMaterial != null && num < 12)
					{
						list3.Add(new
						{
							Name = uUnrealMaterial.Name,
							Path = uUnrealMaterial.GetPathName()
						});
						uUnrealMaterial = (uUnrealMaterial as UMaterialInstance)?.Parent?.Load<UUnrealMaterial>();
						num++;
					}
					list2.Add(new
					{
						Name = uMaterialInterface.Name,
						Chain = list3,
						Scalars = cMaterialParams.Scalars,
						Colors = cMaterialParams.Colors.ToDictionary((KeyValuePair<string, FLinearColor> k) => k.Key, (KeyValuePair<string, FLinearColor> v) => new float[4]
						{
							v.Value.R,
							v.Value.G,
							v.Value.B,
							v.Value.A
						}),
						Switches = cMaterialParams.Switches,
						Textures = cMaterialParams.Textures.Select(x => new KeyValuePair<string, UUnrealMaterial>(x.Key, x.Value.Load() as UUnrealMaterial)).Where(x => x.Value != null).ToDictionary((KeyValuePair<string, UUnrealMaterial> k) => k.Key, (KeyValuePair<string, UUnrealMaterial> v) => (!(v.Value is UTexture { Name: var name } uTexture)) ? ((object)v.Value.Name) : ((object)new
						{
							Name = name,
							Path = uTexture.GetPathName(),
							SRGB = uTexture.SRGB,
							Normal = uTexture.IsNormalMap,
							Format = uTexture.Format.ToString(),
							Width = uTexture.PlatformData.SizeX,
							Height = uTexture.PlatformData.SizeY,
							FirstMipToSerialize = uTexture.PlatformData.FirstMipToSerialize,
							FirstLoadedMip = uTexture.GetFirstMipIndex(),
							Chunks = Chunks(uTexture),
							Mips = uTexture.PlatformData.Mips.Select((FTexture2DMipMap m, int index) => new
							{
								Index = index,
								SizeX = m.SizeX,
								SizeY = m.SizeY,
								Flags = m.BulkData?.BulkDataFlags.ToString(),
								Bytes = m.BulkData?.Header.SizeOnDisk,
								Offset = m.BulkData?.Header.OffsetInFile,
								Cooked = m.BulkData?.Header.CookedIndex.ToString()
							}).ToArray()
						}))
					});
				}
			}
			details.Add(new
			{
				Name = uObject3.Name,
				Lods = meshDto.LODs.Select((MeshLodDto<T> l) => new
				{
					SourceLodIndex = l.SourceLodIndex,
					IsNanite = l.IsNanite,
					Vertices = l.Vertices.Length,
					Triangles = l.Indices.Length / 3,
					Sections = l.Sections.Length
				}).ToArray(),
				Materials = list2
			});
		}
	}

	private IEnumerable<UObject> VariantChannels(string packagePath)
	{
		return from x in _provider.LoadPackage(packagePath).GetExports()
			where x.Properties.Any((FPropertyTag p) => p.Name.Text == "VariantChannelName")
			select x;
	}

	private List<FStructFallback> ChannelOptions(UObject channel)
	{
		return (from x in Structures(channel.Properties)
			where x.Properties.Any((FPropertyTag p) => p.Name.Text == "VariantName")
			select x).ToList();
	}

	public List<CosmeticStyleChannel> GetStyleChannels(Cosmetic cosmetic)
	{
		if (_provider == null || cosmetic.Category == "Animations" || cosmetic.ObjectPath.StartsWith("__"))
		{
			return new List<CosmeticStyleChannel>();
		}
		List<CosmeticStyleChannel> list = new List<CosmeticStyleChannel>();
		foreach (UObject item in VariantChannels(cosmetic.PackagePath ?? cosmetic.ObjectPath))
		{
			string name = TextValue(item.Properties.First((FPropertyTag p) => p.Name.Text == "VariantChannelName").Tag?.GenericValue) ?? "Style";
			List<FStructFallback> list2 = ChannelOptions(item);
			List<CosmeticStyleOption> list3 = new List<CosmeticStyleOption>();
			string selected = "0";
			for (int num = 0; num < list2.Count; num++)
			{
				string text = TextValue(list2[num].Properties.First((FPropertyTag p) => p.Name.Text == "VariantName").Tag?.GenericValue);
				if (!string.IsNullOrWhiteSpace(text))
				{
					list3.Add(new CosmeticStyleOption(num.ToString(), text));
					if (list2[num].GetOrDefault("bIsDefault", defaultValue: false))
					{
						selected = num.ToString();
					}
				}
			}
			FLinearColor[] orDefault = item.GetOrDefault<FLinearColor[]>("BakedSwatchColors");
			if (orDefault != null && orDefault.Length > 0)
			{
				FStructFallback fStructFallback = Structures(item.Properties).FirstOrDefault((FStructFallback x) => x.Properties.Any((FPropertyTag p) => p.Name.Text == "ColorParamName"));
				FLinearColor? fLinearColor = fStructFallback?.GetOrDefault<FLinearColor>("DefaultStartingColor");
				UObject uObject = ((fStructFallback == null) ? null : ReferencedObjects(fStructFallback.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "ColorSwatchForChoices")?.Tag?.GenericValue, new List<string>()).FirstOrDefault());
				FStructFallback[] source = ((uObject == null) ? Array.Empty<FStructFallback>() : (from x in Structures(uObject.Properties)
					where x.Properties.Any((FPropertyTag p) => p.Name.Text == "ColorValue")
					select x).ToArray());
				list3.Clear();
				for (int num2 = 0; num2 < orDefault.Length; num2++)
				{
					FLinearColor color = orDefault[num2];
					string text2 = "#" + Hex(color.R) + Hex(color.G) + Hex(color.B);
					FStructFallback fStructFallback2 = source.FirstOrDefault((FStructFallback x) =>
					{
						FLinearColor orDefault2 = x.GetOrDefault<FLinearColor>("ColorValue");
						return (double)(Math.Abs(orDefault2.R - color.R) + Math.Abs(orDefault2.G - color.G) + Math.Abs(orDefault2.B - color.B)) < 0.0001;
					});
					string text3 = ((fStructFallback2 == null) ? null : (TextValue(fStructFallback2.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "ColorDisplayName")?.Tag?.GenericValue) ?? TextValue(fStructFallback2.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "ColorName")?.Tag?.GenericValue)));
					list3.Add(new CosmeticStyleOption(num2.ToString(), string.IsNullOrWhiteSpace(text3) ? $"Colour {num2 + 1} · {text2}" : text3, text2));
					if (fLinearColor.HasValue)
					{
						FLinearColor valueOrDefault = fLinearColor.GetValueOrDefault();
						if ((double)(Math.Abs(valueOrDefault.R - color.R) + Math.Abs(valueOrDefault.G - color.G) + Math.Abs(valueOrDefault.B - color.B)) < 0.0001)
						{
							selected = num2.ToString();
						}
					}
				}
			}
			if (list3.Count > 0)
			{
				list.Add(new CosmeticStyleChannel(item.Name, name, list3.Any((CosmeticStyleOption x) => x.Id == selected) ? selected : list3[0].Id, list3));
			}
		}
		return list;
		static string Hex(float x)
		{
			return ((int)Math.Round((((double)x <= 0.0031308) ? ((double)x * 12.92) : (1.055 * Math.Pow(Math.Clamp(x, 0f, 1f), 5.0 / 12.0) - 0.055)) * 255.0)).ToString("X2");
		}
	}

	private List<FStructFallback> FindStyleOptions(string packagePath, string selection)
	{
		if (!selection.StartsWith("channels:", StringComparison.Ordinal))
		{
			return (from o in _provider.LoadPackage(packagePath).GetExports().SelectMany((UObject o) => Structures(o.Properties))
				where string.Equals(TextValue(o.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "VariantName")?.Tag?.GenericValue), selection, StringComparison.OrdinalIgnoreCase)
				select o).Take(1).ToList();
		}
		Dictionary<string, string> dictionary = StyleSelection.Decode(selection);
		List<FStructFallback> list = new List<FStructFallback>();
		foreach (UObject item in VariantChannels(packagePath))
		{
			if (!dictionary.Remove(item.Name, out var value))
			{
				continue;
			}
			if (!int.TryParse(value, out var result) || result < 0)
			{
				throw new IOException("Invalid style option. Select the style again.");
			}
			FLinearColor[] orDefault = item.GetOrDefault<FLinearColor[]>("BakedSwatchColors");
			if (orDefault != null && orDefault.Length > 0)
			{
				if (result >= orDefault.Length)
				{
					throw new IOException("Colour option no longer exists.");
				}
				continue;
			}
			List<FStructFallback> list2 = ChannelOptions(item);
			if (result >= list2.Count)
			{
				throw new IOException("Style option no longer exists.");
			}
			list.Add(list2[result]);
		}
		if (dictionary.Count > 0)
		{
			throw new IOException("Style channel no longer exists. Rescan this asset.");
		}
		return list;
	}

	private Dictionary<string, StyleMaterialParameters> ResolveStyleParameters(string packagePath, string style, List<string> warnings)
	{
		Dictionary<string, StyleMaterialParameters> dictionary = new Dictionary<string, StyleMaterialParameters>(StringComparer.OrdinalIgnoreCase);
		if (style.Equals("Default", StringComparison.OrdinalIgnoreCase))
		{
			return dictionary;
		}
		foreach (FStructFallback item in FindStyleOptions(packagePath, style))
		{
			foreach (FStructFallback item2 in from s in Structures(item)
				where s.Properties.Any((FPropertyTag p) =>
				{
					switch (p.Name.Text)
					{
					case "MaterialsToAlter":
					case "MaterialToAlter":
					case "MaterialParams":
						return true;
					default:
						return false;
					}
				})
				select s)
			{
				foreach (FSoftObjectPath item3 in SoftPaths(item2.Properties.FirstOrDefault((FPropertyTag p) =>
				{
					string text4 = p.Name.Text;
					return (text4 == "MaterialsToAlter" || text4 == "MaterialToAlter") ? true : false;
				})?.Tag?.GenericValue))
				{
					string text = item3.AssetPathName.Text;
					if (string.IsNullOrWhiteSpace(text))
					{
						continue;
					}
					StyleMaterialParameters styleMaterialParameters = (dictionary.TryGetValue(text, out var value) ? value : new StyleMaterialParameters());
					foreach (FStructFallback item4 in Structures(item2.Properties.Where((FPropertyTag p) =>
					{
						string text4 = p.Name.Text;
						return (text4 == "FloatParams" || text4 == "ScalarParameterValues") ? true : false;
					})))
					{
						if (!string.IsNullOrWhiteSpace(ParamName(item4)))
						{
							styleMaterialParameters.Scalars[ParamName(item4)] = item4.GetOrDefault("Value", 0f);
						}
					}
					foreach (FStructFallback item5 in Structures(item2.Properties.Where((FPropertyTag p) =>
					{
						string text4 = p.Name.Text;
						return (text4 == "ColorParams" || text4 == "VectorParameterValues") ? true : false;
					})))
					{
						if (!string.IsNullOrWhiteSpace(ParamName(item5)))
						{
							styleMaterialParameters.Colors[ParamName(item5)] = item5.GetOrDefault<FLinearColor>("Value");
						}
					}
					foreach (FStructFallback item6 in Structures(item2.Properties.Where((FPropertyTag p) =>
					{
						string text4 = p.Name.Text;
						return (text4 == "TextureParams" || text4 == "TextureParameterValues") ? true : false;
					})))
					{
						UUnrealMaterial uUnrealMaterial = ReferencedObjects(item6.Properties.FirstOrDefault((FPropertyTag p) =>
						{
							string text4 = p.Name.Text;
							return (text4 == "Value" || text4 == "ParameterValue") ? true : false;
						})?.Tag?.GenericValue, warnings).OfType<UUnrealMaterial>().FirstOrDefault();
						if (uUnrealMaterial != null && !string.IsNullOrWhiteSpace(ParamName(item6)))
						{
							styleMaterialParameters.Textures[ParamName(item6)] = uUnrealMaterial;
						}
					}
					if (!string.IsNullOrWhiteSpace(item3.AssetPathName.Text))
					{
						dictionary[item3.AssetPathName.Text] = styleMaterialParameters;
					}
				}
			}
		}
		if (style.StartsWith("channels:"))
		{
			Dictionary<string, string> dictionary2 = StyleSelection.Decode(style);
			foreach (UObject item7 in VariantChannels(packagePath))
			{
				FLinearColor[] orDefault = item7.GetOrDefault<FLinearColor[]>("BakedSwatchColors");
				if (orDefault == null || orDefault.Length <= 0 || !dictionary2.TryGetValue(item7.Name, out var value2))
				{
					continue;
				}
				if (!int.TryParse(value2, out var result) || result < 0 || result >= orDefault.Length)
				{
					throw new IOException("Invalid colour selection.");
				}
				foreach (FStructFallback item8 in from x in Structures(item7.Properties)
					where x.Properties.Any((FPropertyTag p) => p.Name.Text == "ColorParamName")
					select x)
				{
					string text2 = TextValue(item8.Properties.First((FPropertyTag p) => p.Name.Text == "ColorParamName").Tag?.GenericValue);
					if (string.IsNullOrWhiteSpace(text2))
					{
						continue;
					}
					foreach (FSoftObjectPath item9 in SoftPaths(item8.Properties.FirstOrDefault((FPropertyTag p) => p.Name.Text == "MaterialsToAlter")?.Tag?.GenericValue))
					{
						string text3 = item9.AssetPathName.Text;
						if (!string.IsNullOrWhiteSpace(text3))
						{
							if (!dictionary.TryGetValue(text3, out var value3))
							{
								value3 = (dictionary[text3] = new StyleMaterialParameters());
							}
							value3.Colors[text2] = orDefault[result];
						}
					}
				}
			}
		}
		return dictionary;
		static string ParamName(FStructFallback item)
		{
			return item.GetOrDefault<FName>("ParamName").Text ?? item.GetOrDefault<FName>("ParameterName").Text ?? item.GetOrDefault<FName>("Name").Text;
		}
	}
}
