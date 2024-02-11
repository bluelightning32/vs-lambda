using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Network;

public class InventoryOptions {
  public bool RequireTerm;
  public bool RequireConstructor;
  public bool RequireFunction;
  public int MaxSlotStackSize = 999999;
  public string DialogTitleLangCode;
  public string DialogDescLangCode;
  public bool HidePerishRate;
  public Dictionary<string, CompositeTexture> FullTextures;

  public int GetMaxStackForItem(ItemStack item) {
    CollectibleBehavior.Term term =
        item.Collectible.GetBehavior<CollectibleBehavior.Term>();
    if (RequireTerm) {
      if (term == null) {
        return 0;
      }
    }
    if (RequireFunction) {
      if (!(term?.IsFunction(item) ?? false)) {
        return 0;
      }
    }
    if (RequireConstructor) {
      if (term?.GetConstructs(item) == null) {
        return 0;
      }
    }
    return MaxSlotStackSize;
  }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum PortDirection {
  [EnumMember(Value = "none")] None = 0,
  [EnumMember(Value = "direct-in")] DirectIn = 1,
  [EnumMember(Value = "direct-out")] DirectOut = 2,
  [EnumMember(Value = "passthrough-in")] PassthroughIn = 3,
  [EnumMember(Value = "passthrough-out")] PassthroughOut = 4,
}

public class PortOption {
  public string Name;
  public string Parent;
  public NetworkType Network;
  public PortDirection[] Directions;
  public BlockFacing[] Faces = Array.Empty<BlockFacing>();
  public Dictionary<string, CompositeTexture> FullTextures;

  public InventoryOptions Inventory;

  public PortOption(string name, PortDirection[] directions, string[] faces,
                    Dictionary<string, CompositeTexture> fullTextures,
                    InventoryOptions inventory) {
    faces ??= Array.Empty<string>();
    Name = name;
    Directions = directions;
    Faces = new BlockFacing[faces.Length];
    for (int i = 0; i < faces.Length; ++i) {
      Faces[i] = BlockFacing.FromCode(faces[i]);
      if (Faces[i] == null) {
        throw new ArgumentException($"Bad facing code: {faces[i]}");
      }
    }
    FullTextures = fullTextures;
    Inventory = inventory;
  }
}