using System.Collections.Generic;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Objects.Core.Misc;

namespace FortniteObj.Core;

public record CompatibilityData(string MappingPath, Dictionary<FGuid, FAesKey> Keys, string Version);
