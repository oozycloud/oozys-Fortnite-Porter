using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;

namespace FortniteObj.Core;

public sealed class MaterialExporter(string folder, List<string> warnings, CancellationToken ct, bool matteMaterials = true)
{
	public record TextureInfo(string File, string Source, int Width, int Height, int Mip, string Role);

	public sealed record MaterialInfo(string Name, string Source)
	{
		public Dictionary<string, string> Maps { get; } = new Dictionary<string, string>();

		public float[] Color { get; set; } = new float[4] { 1f, 1f, 1f, 1f };

		public float RoughMin { get; set; }

		public float RoughMax { get; set; } = 1f;

		public float EmissionStrength { get; set; }

		public float Metallic { get; set; }

		public float MetallicOverride { get; set; }

		public bool Matte { get; set; }

		public float Roughness { get; set; } = 0.5f;

		public float Specular { get; set; } = 0.5f;

		public bool CartoonColor { get; set; }

		public bool UseBaseAlpha { get; set; }

		public bool HiddenSurface { get; set; }

		public Dictionary<string, float> Scalars { get; set; } = new Dictionary<string, float>();

		public Dictionary<string, float> HiddenElements { get; set; } = new Dictionary<string, float>();

		public Dictionary<string, bool> Switches { get; set; } = new Dictionary<string, bool>();

		public float[] ShadedColor { get; set; } = new float[4] { 0.4f, 0.4f, 0.4f, 1f };

		public int LayerCount { get; set; } = 1;

		public Dictionary<string, float[]> Colors { get; set; } = new Dictionary<string, float[]>();

		public List<string> MissingMaps { get; } = new List<string>();
	}

	private readonly Dictionary<string, TextureInfo> _textures = new Dictionary<string, TextureInfo>();

	public List<MaterialInfo> Materials { get; } = new List<MaterialInfo>();

	public IEnumerable<TextureInfo> Textures => _textures.Values;

	public void Write(StreamWriter mtl, string name, UMaterialInterface material, StyleMaterialParameters? parameters = null)
	{
		CMaterialParams2 p = new CMaterialParams2();
		material.GetParams(p, EMaterialDepth.AllLayers);
		parameters?.Apply(p);
		CMaterialParams cMaterialParams = new CMaterialParams();
		material.GetParams(cMaterialParams);
		MaterialInfo info = new MaterialInfo(name, material.GetPathName());
		Materials.Add(info);
		bool flag = material.GetPathName().Contains("InvHullOutline", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_outer", StringComparison.OrdinalIgnoreCase);
		info.RoughMin = Math.Clamp(Scalar(0f, new string[2] { "RoughnessMin", "rough min" }), 0f, 1f);
		info.RoughMax = Math.Clamp(Scalar(1f, new string[2] { "RoughnessMax", "rough max" }), info.RoughMin, 1f);
		info.Metallic = Math.Clamp(Scalar(0f, new string[3] { "Metallness", "Metalness", "Metallic" }), 0f, 1f);
		info.MetallicOverride = Math.Clamp(Scalar(0f, new string[1] { "Overwrite Metallic from _S Mask" }), 0f, 1f);
		info.Matte = matteMaterials;
		if (matteMaterials)
		{
			info.Metallic = 0f;
			info.MetallicOverride = 1f;
			info.RoughMin = Math.Max(0.65f, info.RoughMin);
			info.RoughMax = Math.Max(info.RoughMin, info.RoughMax);
			info.Roughness = Math.Max(0.72f, info.RoughMin);
			info.Specular = 0.15f;
		}
		info.HiddenElements = p.Scalars.Where((KeyValuePair<string, float> x) => x.Key.StartsWith("Hide Element", StringComparison.OrdinalIgnoreCase)).ToDictionary((KeyValuePair<string, float> x) => x.Key, (KeyValuePair<string, float> x) => x.Value);
		info.Switches = p.Switches.ToDictionary((KeyValuePair<string, bool> x) => x.Key, (KeyValuePair<string, bool> x) => x.Value);
		info.Scalars = p.Scalars.ToDictionary((KeyValuePair<string, float> x) => x.Key, (KeyValuePair<string, float> x) => x.Value);
		info.Colors = p.Colors.ToDictionary((KeyValuePair<string, FLinearColor> x) => x.Key, (KeyValuePair<string, FLinearColor> x) => new float[4]
		{
			x.Value.R,
			x.Value.G,
			x.Value.B,
			x.Value.A
		});
		info.HiddenSurface = Scalar(0f, new string[1] { "HT_CrunchVerts" }) >= 0.5f;
		MaterialInfo materialInfo = info;
		EBlendMode blendMode = p.BlendMode;
		bool useBaseAlpha = blendMode - 1 <= EBlendMode.BLEND_Masked;
		materialInfo.UseBaseAlpha = useBaseAlpha;
		if (info.HiddenSurface)
		{
			return;
		}
		if (flag)
		{
			info.Color = new float[4] { 0.012f, 0.012f, 0.012f, 1f };
			info.Roughness = 1f;
			info.Metallic = 0f;
			info.MetallicOverride = 1f;
			info.Specular = 0f;
		}
		if (!flag && cMaterialParams.Diffuse == null)
		{
			FLinearColor? diffuseColor = cMaterialParams.DiffuseColor;
			if (diffuseColor.HasValue)
			{
				FLinearColor valueOrDefault = diffuseColor.GetValueOrDefault();
				info.Color = new float[4] { valueOrDefault.R, valueOrDefault.G, valueOrDefault.B, 1f };
			}
		}
		mtl.WriteLine(FormattableString.Invariant($"Kd {info.Color[0]} {info.Color[1]} {info.Color[2]}\nKs {info.Specular} {info.Specular} {info.Specular}\nPr {info.Roughness}\nPm {info.Metallic}\nNs {(matteMaterials ? 2 : 30)}"));
		if (flag)
		{
			info.Colors["OuterLayer"] = info.Color;
			return;
		}
		bool flag2 = p.Switches.Any((KeyValuePair<string, bool> x) =>
		{
			bool flag3 = x.Value;
			if (flag3)
			{
				bool flag4;
				switch (Normalize(x.Key))
				{
				case "ISTOONMATERIAL":
				case "USESHADINGTEXTURE":
				case "USECOLORLITMAP":
					flag4 = true;
					break;
				default:
					flag4 = false;
					break;
				}
				flag3 = flag4;
			}
			return flag3;
		});
		UUnrealMaterial uUnrealMaterial = (flag2 ? Texture(new string[3] { "Color_Lit_Map", "LitDiffuse", "Lit_Diffuse" }) : null);
		info.CartoonColor = flag2 || uUnrealMaterial != null;
		UUnrealMaterial uUnrealMaterial2 = uUnrealMaterial ?? Texture(new string[10] { "PM_Diffuse", "Diffuse", "BaseColor", "Base Colour", "Albedo", "DiffuseTexture", "Diffuse Map", "Base Color Texture", "Trunk_BaseColor", "LitDiffuse" }) ?? cMaterialParams.Diffuse?.Load<UUnrealMaterial>();
		Map("BaseColor", "map_Kd", uUnrealMaterial2);
		if (p.Colors.ContainsKey("Primary Color") && p.Colors.ContainsKey("Secondary Color") && p.Colors.ContainsKey("Accent Color"))
		{
			Map("CustomizationPattern", "# customization_pattern", Texture(new string[1] { "SkinFX_Mask" }));
		}
		if (p.Scalars.ContainsKey("Jacket Pan Speed X") && p.Colors.ContainsKey("Jacket channel"))
		{
			Map("JacketPattern", "# animated_jacket", Texture(new string[1] { "GalaxyTexture" }));
			Map("JacketMask", "# jacket_mask", Texture(new string[1] { "Mask" }));
			Map("JacketGradient", "# jacket_colour_curve", ReferencedTexture("AthenaCurveAtlas01"));
		}
		if (p.Colors.ContainsKey("Universe") && Texture(new string[1] { "SkinFX2" }) != null)
		{
			Map("Planet", "# planet_surface", Texture(new string[1] { "SkinFX2" }));
			Map("PlanetColor", "# rotating_planet", Texture(new string[1] { "Planet_Diffuse" }) ?? ReferencedTexture("Planet_Diffuse"));
		}
		if (p.Switches.Any((KeyValuePair<string, bool> x) => x.Value && Normalize(x.Key) == "USEGALAXY"))
		{
			Map("Galaxy", "# galaxy", Texture(new string[1] { "GalaxyTexture" }));
			Map("Stars", "# galaxy_stars", Texture(new string[1] { "Stars" }));
			Map("GalaxyMask", "# galaxy_mask", Texture(new string[1] { "SkinFX_Mask" }));
		}
		if (p.Switches.Any((KeyValuePair<string, bool> x) => x.Value && Normalize(x.Key) == "USESUBUVTEXTURE"))
		{
			Map("FaceAtlas", "# face_atlas", Texture(new string[1] { "Flipbook" }));
			Map("FaceMask", "# face_mask", Texture(new string[1] { "M" }));
		}
		if (info.CartoonColor)
		{
			if (p.Colors.TryGetValue("Color_Shaded", out var value))
			{
				info.ShadedColor = new float[4] { value.R, value.G, value.B, 1f };
			}
			Map("ShadedColor", "# shaded_color", Texture(new string[3] { "Color_Shaded_Map", "ShadedDiffuse", "Shaded_Diffuse" }));
			Map("LineMask", "# line_mask", Texture(new string[3] { "DFL_Map", "DistanceField_InkLines", "DistanceFieldInkLines" }));
			Map("LineColor", "# line_color", Texture(new string[2] { "DFL_Color_Map", "DistanceField_InkLines_Color" }));
		}
		Map("Normal", "map_Bump", Texture(new string[5] { "PM_Normals", "Normals", "Normal", "NormalMap", "NormalTexture" }) ?? cMaterialParams.Normal?.Load<UUnrealMaterial>());
		KeyValuePair<string, FLinearColor>? keyValuePair = parameters?.Colors.FirstOrDefault((KeyValuePair<string, FLinearColor> x) => Normalize(x.Key) == "EMISSIVECOLOR");
		if (keyValuePair.HasValue)
		{
			KeyValuePair<string, FLinearColor> valueOrDefault2 = keyValuePair.GetValueOrDefault();
			if (valueOrDefault2.Key != null && Texture(new string[1] { "Emissive" }) is UTexture)
			{
				info.Colors["SelectedLightColor"] = new float[4]
				{
					valueOrDefault2.Value.R,
					valueOrDefault2.Value.G,
					valueOrDefault2.Value.B,
					1f
				};
				Map("StyleLightMask", "# selected_light_mask", Texture(new string[1] { "Emissive" }));
			}
		}
		if (Texture(new string[2] { "PM_SpecularMasks", "SpecularMasks" }) is UTexture value2)
		{
			if (!matteMaterials)
			{
				Map("Specular", "map_Ks", value2);
			}
			if (info.MetallicOverride < 1f)
			{
				Map("Metallic", "map_Pm", value2);
			}
			Map("Roughness", "map_Pr", value2);
		}
		if (info.UseBaseAlpha)
		{
			UUnrealMaterial uUnrealMaterial3 = Texture(new string[2] { "OpacityMask", "Opacity" });
			if (uUnrealMaterial3 != null && uUnrealMaterial3 != uUnrealMaterial2)
			{
				Map("Alpha", "map_d", uUnrealMaterial3);
			}
		}
		int layer;
		for (layer = 2; layer <= 6; layer++)
		{
			if (p.Switches.Any((KeyValuePair<string, bool> x) => Normalize(x.Key) == $"USE{layer}MATERIALS" && x.Value))
			{
				info.LayerCount = Math.Max(info.LayerCount, layer);
			}
		}
		for (int num = 2; num <= info.LayerCount; num++)
		{
			Map($"BaseColor{num}", "# layer_base_color", Texture(new string[1] { $"Diffuse_Texture_{num}" }));
			Map($"Normal{num}", "# layer_normal", Texture(new string[1] { $"Normals_Texture_{num}" }));
			Map($"Roughness{num}", "# layer_roughness", Texture(new string[1] { $"SpecularMasks_{num}" }));
		}
		if (!info.Maps.ContainsKey("BaseColor"))
		{
			FLinearColor? diffuseColor = cMaterialParams.DiffuseColor;
			if (!diffuseColor.HasValue)
			{
				warnings.Add(material.Name + ": no base colour texture or constant colour was resolved.");
			}
		}
		void Map(string role, string field, UUnrealMaterial? uUnrealMaterial4)
		{
			try
			{
				WriteMap(role, field, uUnrealMaterial4);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				info.MissingMaps.Add(role);
				warnings.Add($"{material.Name}: {role} texture {uUnrealMaterial4?.Name} could not be exported: {ex.Message}");
			}
		}
		static string Normalize(string source)
		{
			return new string(source.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
		}
		UTexture? ReferencedTexture(string text)
		{
			try
			{
				UUnrealMaterial uUnrealMaterial4 = material;
				int num2 = 0;
				while (uUnrealMaterial4 != null && num2 < 16)
				{
					ct.ThrowIfCancellationRequested();
					FPackageIndex[] array = uUnrealMaterial4.GetOrDefault<FPackageIndex[]>("ReferencedDefaultTextures") ?? Array.Empty<FPackageIndex>();
					foreach (FPackageIndex fPackageIndex in array)
					{
						if (fPackageIndex?.ResolvedObject?.Name.Text == text && fPackageIndex.Load() is UTexture result)
						{
							return result;
						}
					}
					uUnrealMaterial4 = (uUnrealMaterial4 as UMaterialInstance)?.Parent?.Load<UUnrealMaterial>();
					num2++;
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				warnings.Add($"{material.Name}: effect texture {text} could not be loaded: {ex.Message}");
			}
			return null;
		}
		float Scalar(float fallback, params string[] names)
		{
			foreach (string text in names)
			{
				foreach (KeyValuePair<string, float> scalar in p.Scalars)
				{
					if (string.Equals(scalar.Key.Replace(" ", ""), text.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
					{
						return scalar.Value;
					}
				}
			}
			return fallback;
		}
		UUnrealMaterial? Texture(params string[] names)
		{
			foreach (string key in names)
			{
				if (p.Textures.TryGetValue(key, out var reference) && reference.Load() is UTexture value3)
				{
					return value3;
				}
				var keyValuePair2 = p.Textures.FirstOrDefault(x => Normalize(x.Key) == Normalize(key));
				if (keyValuePair2.Value?.Load() is UTexture matchedTexture)
				{
					return matchedTexture;
				}
			}
			return null;
		}
		void WriteMap(string role, string field, UUnrealMaterial? uUnrealMaterial4)
		{
			if (uUnrealMaterial4 is UTexture uTexture)
			{
				ct.ThrowIfCancellationRequested();
				string pathName = uTexture.GetPathName();
				string text = role;
				string? text2;
				if (role.StartsWith("Roughness"))
				{
					text2 = $"|{info.RoughMin}|{info.RoughMax}";
				}
				else
				{
					text2 = ((role == "Metallic") ? $"|{info.Metallic}|{info.MetallicOverride}" : "");
				}
				string key = pathName + "|" + text + text2;
				if (!_textures.TryGetValue(key, out TextureInfo value3))
				{
					CTexture cTexture = uTexture.Decode() ?? throw new IOException("Cannot decode " + uTexture.Name);
					byte[] array = cTexture.Encode(ETextureFormat.Png, saveHdrAsHdr: false, out string _) ?? throw new IOException("Empty texture " + uTexture.Name);
					bool flag3 = role.StartsWith("Normal") || role.StartsWith("Roughness");
					if (!flag3)
					{
						bool flag4 = ((role == "Specular" || role == "Metallic") ? true : false);
						flag3 = flag4;
					}
					if (flag3)
					{
						using SKBitmap sKBitmap = SKBitmap.Decode(array);
						SKColor[] pixels = sKBitmap.Pixels;
						for (int i = 0; i < pixels.Length; i++)
						{
							SKColor sKColor = pixels[i];
							if (role.StartsWith("Normal"))
							{
								pixels[i] = new SKColor(sKColor.Red, (byte)(255 - sKColor.Green), sKColor.Blue, byte.MaxValue);
							}
							else
							{
								byte b;
								if (role == "Specular")
								{
									b = sKColor.Red;
								}
								else
								{
									b = ((role == "Metallic") ? ((byte)Math.Clamp((int)Math.Round((float)(int)sKColor.Green * (1f - info.MetallicOverride) + 255f * info.Metallic * info.MetallicOverride), 0, 255)) : ((byte)Math.Clamp((int)Math.Round(255f * info.RoughMin + (float)(int)sKColor.Blue * (info.RoughMax - info.RoughMin)), 0, 255)));
								}
								pixels[i] = new SKColor(b, b, b, byte.MaxValue);
							}
						}
						sKBitmap.Pixels = pixels;
						using SKData sKData = sKBitmap.Encode(SKEncodedImageFormat.Png, 100);
						array = sKData.ToArray();
					}
					string text3 = $"textures/{_textures.Count:D4}_{Installation.SafeName(uTexture.Name)}_{role}.png";
					Directory.CreateDirectory(Path.Combine(folder, "textures"));
					File.WriteAllBytes(Path.Combine(folder, text3), array);
					value3 = new TextureInfo(text3, uTexture.GetPathName(), cTexture.Width, cTexture.Height, uTexture.GetFirstMipIndex(), role);
					_textures.Add(key, value3);
					if (value3.Mip > 0)
					{
						warnings.Add($"{uTexture.Name}: only mip {value3.Mip} ({value3.Width}×{value3.Height}) was available; the largest cooked mip is missing.");
					}
				}
				info.Maps[role] = value3.File;
				mtl.WriteLine(field + " " + value3.File);
			}
		}
	}
}
