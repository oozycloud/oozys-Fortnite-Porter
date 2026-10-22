using System.Collections.Generic;

namespace FortniteObj.Core;

public record CosmeticStyleChannel(string Id, string Name, string DefaultOption, List<CosmeticStyleOption> Options);
