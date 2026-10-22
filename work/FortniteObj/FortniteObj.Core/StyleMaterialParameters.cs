using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;

namespace FortniteObj.Core;

public sealed class StyleMaterialParameters
{
	public Dictionary<string, float> Scalars { get; } = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, FLinearColor> Colors { get; } = new Dictionary<string, FLinearColor>(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, UUnrealMaterial> Textures { get; } = new Dictionary<string, UUnrealMaterial>(StringComparer.OrdinalIgnoreCase);

	public void Apply(CMaterialParams2 p)
	{
		foreach (KeyValuePair<string, float> scalar in Scalars)
		{
			p.Scalars[scalar.Key] = scalar.Value;
		}
		foreach (KeyValuePair<string, FLinearColor> color in Colors)
		{
			p.Colors[color.Key] = color.Value;
		}
		foreach (KeyValuePair<string, UUnrealMaterial> texture in Textures)
		{
			p.Textures[texture.Key] = new CUE4Parse.UE4.Assets.ResolvedLoadedObject(texture.Value);
		}
	}
}
