using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

[JsonConverter(typeof(StringEnumConverter))]
public enum PortDirection {
  [EnumMember(Value = "none")] None = 0,
  [EnumMember(Value = "in")] In = 1,
  [EnumMember(Value = "out")] Out = 2,
}

public class PortOption {
  public string Name;
  public PortDirection[] Directions;
  public BlockFacing[] Faces = Array.Empty<BlockFacing>();
  public Dictionary<string, CompositeTexture> FullTextures;

  public PortOption(string name, PortDirection[] directions, string[] faces,
                    Dictionary<string, CompositeTexture> fullTextures) {
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
  }
}

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

public interface IAcceptPorts {
  bool SetPort(Block port, PortDirection direction, BlockFacing face,
               out string failureCode);
}

public class BEBehaviorAcceptPorts : BEBehaviorTermNetwork, IAcceptPorts {
  // Each face uses 1 bit to indicate whether a port is present.
  private int _portedSides = 0;
  // Each face uses 2 bits to indicate which kind of port is present.
  private int _occupiedPorts = 0;
  PortConfiguration _configuration;

  public BEBehaviorAcceptPorts(BlockEntity blockentity) : base(blockentity) {}

  public static PortConfiguration ParseConfiguration(ICoreAPI api,
                                                     JsonObject properties) {
    Dictionary<JsonObject, PortConfiguration> cache =
        ObjectCacheUtil.GetOrCreate(
            api, $"lambdafactory-accept-ports",
            () => new Dictionary<JsonObject, PortConfiguration>());
    if (cache.TryGetValue(properties, out PortConfiguration configuration)) {
      return configuration;
    }
    api.Logger.Debug(
        "lambda: Accept ports properties cache miss. Dict has {0} entries.",
        cache.Count);
    configuration = properties.AsObject<PortConfiguration>(
        new PortConfiguration(Array.Empty<PortOption>()),
        LambdaFactoryModSystem.Domain);
    cache.Add(properties, configuration);
    return configuration;
  }

  private BlockNodeTemplate ParseBlockNodeTemplate(IWorldAccessor world,
                                                   JsonObject properties,
                                                   int occupiedPorts) {
    return GetManager(world.Api).ParseAcceptPortsTemplate(properties,
                                                          occupiedPorts);
  }

  protected override BlockNodeTemplate
  ParseBlockNodeTemplate(IWorldAccessor world, JsonObject properties) {
    return ParseBlockNodeTemplate(world, properties, _occupiedPorts);
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
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
        BlockBehaviorPort port = decors[i]?.GetBehavior<BlockBehaviorPort>();
        if (port != null) {
          _portedSides |= 1 << i;
          _occupiedPorts |= (int)port.Direction << (i << 1);
        }
      }
    }
    _template = ParseBlockNodeTemplate(Api.World, properties);
  }

  public void EditMesh(MeshData mesh) { CutPortHoles(_portedSides, mesh); }

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

  public override object GetKey() { return _portedSides; }

  public override object GetImmutableKey() { return _portedSides; }

  public virtual bool CanAcceptPort(PortOption option, PortDirection direction,
                                    BlockFacing face, out string failureCode) {
    // Verify the port option isn't full from a different side.
    foreach (BlockFacing faceOption in option.Faces) {
      if ((_portedSides & (1 << faceOption.Index)) != 0) {
        failureCode = "portfull";
        return false;
      }
    }
    BlockNodeTemplate newTemplate = ParseBlockNodeTemplate(
        Api.World, properties,
        _occupiedPorts | ((int)direction << (face.Index << 1)));
    return newTemplate.CanPlace(Pos, out failureCode);
  }

  public bool SetPort(Block port, PortDirection direction, BlockFacing face,
                      out string failureCode) {
    if (!_configuration.FaceIndex.TryGetValue(face,
                                              out PortOption portOption)) {
      failureCode = "noporthere";
      return false;
    }
    if (!portOption.Directions.Contains(direction)) {
      failureCode = "wrongdirection";
      return false;
    }
    if (!CanAcceptPort(portOption, direction, face, out failureCode)) {
      return false;
    }
    if (Api.World.BlockAccessor.SetDecor(port, Pos, face)) {
      _portedSides |= 1 << face.Index;
      _occupiedPorts |= (int)direction << (face.Index << 1);
      _template = ParseBlockNodeTemplate(Api.World, properties);
      _template.SetSourceScope(Pos, _nodes);
      int nodeId =
          _template.GetNodeTemplate(EdgeExtension.GetFaceCenter(face)).Id;
      _template.OnNodePlaced(Pos, nodeId, ref _nodes[nodeId]);
      (Blockentity as BlockEntityCacheMesh)?.UpdateMesh();
      return true;
    }
    failureCode = "existingdecorinplace";
    return false;
  }

  public override TextureAtlasPosition GetTexture(string textureCode) {
    foreach (PortOption option in _configuration.Ports) {
      if (option.FullTextures == null ||
          !option.FullTextures.TryGetValue(textureCode,
                                           out CompositeTexture replacement)) {
        continue;
      }
      ICoreClientAPI capi = (ICoreClientAPI)Api;
      foreach (BlockFacing faceOption in option.Faces) {
        if ((_portedSides & (1 << faceOption.Index)) != 0) {
          replacement.Bake(capi.Assets);
          ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
          atlas.GetOrInsertTexture(
              replacement.Baked.BakedName, out int id,
              out TextureAtlasPosition tex,
              () => atlas.LoadCompositeBitmap(replacement.Baked.BakedName));
          return tex;
        }
      }
    }
    return null;
  }
}