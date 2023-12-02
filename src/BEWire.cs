using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class BlockEntityWire : BlockEntity, IBlockEntityForward {
  private static MeshData _up_mesh = null;
  private int _directions = 63;
  private Cuboidf[] _selectionBoxes = null;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    LoadUpMesh();
    UpdateSelectionBoxes();
  }

  private void LoadUpMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    Api.Logger.Notification($"lambda: LoadUpMesh {GetHashCode()}");
    if (_up_mesh != null) {
      return;
    }
    Shape shape = Shape.TryGet(
        Api, new AssetLocation("lambdafactory", "shapes/block/wire/up.json"));
    ICoreClientAPI capi = (ICoreClientAPI)Api;
    capi.Tesselator.TesselateShape(Block, shape, out _up_mesh);
  }

  private void UpdateSelectionBoxes() {
    Dictionary<int, Cuboidf[]> cache = ObjectCacheUtil.GetOrCreate(
        Api, "lambdafactory-wire-collisionSelectionBoxes",
        () => new Dictionary<int, Cuboidf[]>());
    if (cache.TryGetValue(_directions, out _selectionBoxes)) {
      return;
    }
    cache[_directions] = _selectionBoxes = GenerateSelectionBoxes();
  }

  public Cuboidf[] GenerateSelectionBoxes() {
    List<Cuboidf> boxes = new();
    Cuboidf center = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                 9.5f / 16, 9.5f / 16);
    int remaining = _directions;
    for (int i = 0; i < BlockFacing.indexUP; ++i) {
      int j = BlockFacing.ALLFACES[i].Opposite.Index;
      if ((remaining & (1 << i)) != 0 && (remaining & (1 << j)) != 0) {
        center.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
        center.Expand(BlockFacing.ALLFACES[j], 6.5f / 16);
        boxes.Add(center);
        remaining &= ~((1 << i) | (1 << j));
        center = null;
        break;
      }
    }
    for (int i = 0; i < 6; ++i) {
      if ((remaining & (1 << i)) != 0) {
        if (center != null) {
          center.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
          boxes.Add(center);
          center = null;
        } else {
          Cuboidf side = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                     9.5f / 16, 9.5f / 16);
          side.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
          side.Expand(BlockFacing.ALLFACES[i].Opposite, -3f / 16);
          boxes.Add(side);
        }
      }
    }
    if (center != null) {
      boxes.Add(center);
    }
    return boxes.ToArray();
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    Api.Logger.Notification($"lambda: OnTesselation {GetHashCode()}");
    if (_up_mesh == null) {
      return false;
    }
    float[] rotation = Mat4f.Create();
    Vec3f origin = new(0.5f, 0.5f, 0.5f);
    for (int i = 0; i < 6; ++i) {
      if ((_directions & (1 << i)) != 0) {
        Mat4f.Identity(rotation);
        Mat4f.Translate(rotation, rotation, 0.5f, 0.5f, 0.5f);
        switch (i) {
        case BlockFacing.indexNORTH:
          Mat4f.RotateX(rotation, rotation, -MathF.PI / 2);
          break;
        case BlockFacing.indexEAST:
          Mat4f.RotateZ(rotation, rotation, -MathF.PI / 2);
          break;
        case BlockFacing.indexSOUTH:
          Mat4f.RotateX(rotation, rotation, MathF.PI / 2);
          break;
        case BlockFacing.indexWEST:
          Mat4f.RotateZ(rotation, rotation, MathF.PI / 2);
          break;
        case BlockFacing.indexUP:
          break;
        case BlockFacing.indexDOWN:
          Mat4f.RotateZ(rotation, rotation, MathF.PI);
          break;
        }
        Mat4f.Translate(rotation, rotation, -0.5f, -0.5f, -0.5f);
        mesher.AddMeshData(_up_mesh, rotation);
      }
    }
    return false;
  }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public override void OnExchanged(Block block) {
    Api.Logger.Notification($"lambda: OnExchanged {GetHashCode()}");
    base.OnExchanged(block);
  }
}
