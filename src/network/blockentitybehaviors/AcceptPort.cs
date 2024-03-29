using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.BlockEntities;
using Lambda.BlockEntityBehaviors;
using Lambda.Network.BlockBehaviors;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Network.BlockEntityBehaviors;

[JsonObject(MemberSerialization.OptIn)]
public class PortConfiguration {
  [JsonProperty]
  public readonly PortOption[] Ports;
  private readonly Dictionary<BlockFacing, PortOption> _faceIndex =
      new Dictionary<BlockFacing, PortOption>();
  public IReadOnlyDictionary<BlockFacing, PortOption> FaceIndex {
    get { return _faceIndex; }
  }

  public PortConfiguration(PortOption[] ports) {
    if (ports == null) {
      ports = Array.Empty<PortOption>();
    }
    Ports = ports;
    foreach (PortOption port in ports) {
      foreach (BlockFacing face in port.Faces) {
        _faceIndex.Add(face, port);
      }
    }
  }
}

public class AcceptPort : TokenEmitter, IAcceptPort, IInventoryControl {
  // Each face uses 1 bit to indicate whether a port is present.
  private int _portedSides = 0;
  // Each face uses 3 bits to indicate which kind of port is present.
  private int _occupiedPorts = 0;
  private PortConfiguration _configuration;

  public AcceptPort(BlockEntity blockentity) : base(blockentity) {}

  private static PortConfiguration ParseConfiguration(ICoreAPI api,
                                                      JsonObject properties) {
    Dictionary<JsonObject, PortConfiguration> cache =
        ObjectCacheUtil.GetOrCreate(
            api, $"lambda-accept-ports",
            () => new Dictionary<JsonObject, PortConfiguration>());
    if (cache.TryGetValue(properties, out PortConfiguration configuration)) {
      return configuration;
    }
    configuration = properties.AsObject<PortConfiguration>(
        new PortConfiguration(Array.Empty<PortOption>()), CoreSystem.Domain);
    cache.Add(properties, configuration);
    return configuration;
  }

  private BlockNodeTemplate ParseBlockNodeTemplate(IWorldAccessor world,
                                                   JsonObject properties,
                                                   int occupiedPorts) {
    return GetManager(world.Api).ParseBlockNodeTemplate(properties,
                                                        occupiedPorts, 0);
  }

  protected override BlockNodeTemplate
  ParseBlockNodeTemplate(IWorldAccessor world, JsonObject properties) {
    return ParseBlockNodeTemplate(world, properties, _occupiedPorts);
  }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _configuration = ParseConfiguration(api, properties);

    SetKey();
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    SetKey();
  }

  protected virtual void SetKey() {
    Block[] decors = Api.World.BlockAccessor.GetDecors(Pos);
    _portedSides = 0;
    // A null decors array indicates that none of the faces have a decor. See
    // https://github.com/anegostudios/vsapi/issues/16.
    if (decors != null) {
      for (int i = 0; i < BlockFacing.ALLFACES.Length; ++i) {
        Port port = decors[i]?.GetBehavior<Port>();
        if (port != null) {
          _portedSides |= 1 << i;
          _occupiedPorts |= (int)port.Direction
                            << (i * AutoStepManager.OccupiedPortsBitsPerFace);
        }
      }
    }
    _template = ParseBlockNodeTemplate(Api.World, properties);
  }

  public override void EditMesh(MeshData mesh) {
    CutPortHoles(_portedSides, mesh);
    base.EditMesh(mesh);
  }

  public static void CutPortHoles(int sides, MeshData mesh) {
    if (mesh.VerticesPerFace != 4 || mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    if (sides == 0) {
      return;
    }
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    for (int face = 0; face < origFaceCount; face++) {
      MeshUtil.GetFaceBounds(faceBounds, mesh.xyz, face * mesh.VerticesPerFace,
                             (face + 1) * mesh.VerticesPerFace);
      faceBounds.OmniGrowBy(0.001f);
      for (int i = 0; i < 6; ++i) {
        if ((sides & (1 << i)) == 0) {
          continue;
        }
        BlockFacing facing = BlockFacing.ALLFACES[i];
        if (faceBounds[(int)facing.Axis + 3] - faceBounds[(int)facing.Axis] <
                0.1f &&
            faceBounds.Contains(facing.PlaneCenter.X, facing.PlaneCenter.Y,
                                facing.PlaneCenter.Z)) {
          MeshUtil.AddFaceHole(mesh, facing.Axis, face, facing);
        }
      }
    }
  }

  public override object GetKey() {
    ulong key = _template.GetTextureKey(_nodes, out int bits);
    key |= (ulong)_portedSides << bits;
    bits += 6;
    bool inventoryFull =
        (Blockentity as SingleTermContainer)?.Inventory[0].Itemstack != null;
    key |= (inventoryFull ? 1ul : 0) << bits;
    ++bits;
    if (bits > 64) {
      throw new Exception(
          $"Block has more than the max supported networks with texture overrides.");
    }
    return key;
  }

  public override object GetImmutableKey() { return GetKey(); }

  protected virtual bool CanAcceptPort(PortOption option,
                                       PortDirection direction,
                                       BlockFacing face,
                                       out string failureCode) {
    if (!option.Directions.Contains(direction)) {
      failureCode = "wrongdirection";
      return false;
    }
    // Verify the port option isn't full from a different side.
    if (IsPortFull(option)) {
      failureCode = "portfull";
      return false;
    }
    if (GetInventoryPort() == option) {
      ItemStack item =
          (Blockentity as SingleTermContainer)?.Inventory[0].Itemstack;
      if (item != null &&
          (GetNextInventoryPort()?.Inventory.GetMaxStackForItem(item) ?? 0) <
              item.StackSize) {
        failureCode = "portinventoryfull";
        return false;
      }
    }
    BlockNodeTemplate newTemplate = ParseBlockNodeTemplate(
        Api.World, properties,
        _occupiedPorts |
            ((int)direction
             << (face.Index * AutoStepManager.OccupiedPortsBitsPerFace)));
    return newTemplate.CanPlace(Pos, out failureCode);
  }

  public bool SetPort(Block port, PortDirection direction, BlockFacing face,
                      out string failureCode) {
    if (!_configuration.FaceIndex.TryGetValue(face,
                                              out PortOption portOption)) {
      failureCode = "noporthere";
      return false;
    }
    if (!CanAcceptPort(portOption, direction, face, out failureCode)) {
      return false;
    }
    if (Api.World.BlockAccessor.SetDecor(port, Pos, face)) {
      InventoryOptions oldInventory = GetInventoryOptions();
      _portedSides |= 1 << face.Index;
      _occupiedPorts |= (int)direction
                        << (face.Index *
                            AutoStepManager.OccupiedPortsBitsPerFace);
      _template = ParseBlockNodeTemplate(Api.World, properties);
      _template.SetSourceScope(Pos, _nodes);
      int nodeId = _template
                       .GetNodeTemplate(portOption.Network,
                                        EdgeExtension.GetFaceCenter(face))
                       .Id;
      _template.OnNodePlaced(Pos, nodeId, ref _nodes[nodeId]);
      Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
      if (GetInventoryOptions() != oldInventory) {
        (Blockentity as SingleTermContainer)?.InventoryChanged();
      }
      return true;
    }
    failureCode = "existingdecorinplace";
    return false;
  }

  private bool IsPortFull(PortOption option) {
    foreach (BlockFacing faceOption in option.Faces) {
      if ((_portedSides & (1 << faceOption.Index)) != 0) {
        return true;
      }
    }
    return false;
  }

  private CompositeTexture GetReplacementTexture(string textureCode,
                                                 PortOption option) {
    Dictionary<string, CompositeTexture> replacementDict = null;
    if (IsPortFull(option)) {
      replacementDict = option.FullTextures;
    } else if (option == GetInventoryPort()) {
      if (IsPortInventoryFull(option)) {
        replacementDict = option.Inventory?.FullTextures;
      } else {
        replacementDict = option.Inventory?.EmptyTextures;
      }
    }
    if (replacementDict != null &&
        replacementDict.TryGetValue(textureCode,
                                    out CompositeTexture replacement)) {
      return replacement;
    }
    return null;
  }

  public override TextureAtlasPosition GetTexture(string textureCode) {
    foreach (PortOption option in _configuration.Ports) {
      CompositeTexture replacement = GetReplacementTexture(textureCode, option);
      if (replacement != null) {
        ICoreClientAPI capi = (ICoreClientAPI)Api;
        replacement.Bake(capi.Assets);
        ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
        atlas.GetOrInsertTexture(
            replacement.Baked.BakedName, out int id,
            out TextureAtlasPosition tex,
            () => atlas.LoadCompositeBitmap(replacement.Baked.BakedName));
        return tex;
      }
    }
    return base.GetTexture(textureCode);
  }

  bool IsPortInventoryFull(PortOption option) {
    if (option != GetInventoryPort()) {
      return false;
    }
    return (Blockentity as SingleTermContainer)?.Inventory[0].Itemstack != null;
  }

  PortOption GetInventoryPort() {
    foreach (PortOption option in _configuration.Ports) {
      if (option.Inventory != null && !IsPortFull(option)) {
        return option;
      }
    }
    return null;
  }

  PortOption GetNextInventoryPort() {
    bool foundFirst = false;
    foreach (PortOption option in _configuration.Ports) {
      if (option.Inventory != null && !IsPortFull(option)) {
        if (foundFirst) {
          return option;
        }
        foundFirst = true;
      }
    }
    return null;
  }

  InventoryOptions GetInventoryOptions() {
    return GetInventoryPort()?.Inventory;
  }

  string IInventoryControl.GetTitle() {
    return GetInventoryOptions()?.DialogTitleLangCode;
  }

  string IInventoryControl.GetDescription() {
    return GetInventoryOptions()?.DialogDescLangCode;
  }

  bool IInventoryControl.GetHidePerishRate() {
    return GetInventoryOptions()?.HidePerishRate ?? false;
  }

  void IInventoryControl.OnSlotModified() {
    if ((GetInventoryOptions()?.FullTextures?.Count ?? 0) != 0) {
      Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
      if (Api.Side == EnumAppSide.Client) {
        Blockentity.MarkDirty(true);
      }
    }
  }

  int IInventoryControl.GetMaxStackForItem(ItemStack item) {
    return GetInventoryOptions()?.GetMaxStackForItem(item) ?? 0;
  }

  public override string GetInventoryTerm(out string[] imports) {
    if (Blockentity is SingleTermContainer container) {
      return container.GetInventoryTerm(out imports);
    } else {
      imports = Array.Empty<string>();
      return null;
    }
  }
}
