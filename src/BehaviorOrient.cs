using System;
using System.Collections.Generic;

using Newtonsoft.Json.Serialization;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

enum OrientationMode { Slab, Horizontals, HorizontalsFlipped, Pillar }

// Forwards more methods from the Block to the BlockEntity.
public class BlockBehaviorOrient : BlockBehavior {
  private OrientationMode _mode;
  // If true, rotate the block so that it connects to the selected block face.
  private bool _connectToSelected;
  // If true, rotate the block so that it connects to any of the neighbors.
  private bool _connectToAny;

  public BlockBehaviorOrient(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    // `AsObject` converts the token into a string without the quotes, and
    // Newtonsoft fails to parse that back as an enum. So instead use the Token
    // directly.
    _mode = properties["mode"].Token?.ToObject<OrientationMode>() ??
            OrientationMode.Slab;
    _connectToSelected = properties["connectToSelected"].AsBool(true);
    _connectToAny = properties["connectToAny"].AsBool(true);
  }

  public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     ItemStack itemstack,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    BlockFacing face = blockSel.Face;
    if (_mode == OrientationMode.Slab) {
      double axis1 = blockSel.HitPosition[((int)blockSel.Face.Axis + 1) % 3];
      double axis2 = blockSel.HitPosition[((int)blockSel.Face.Axis + 2) % 3];
      bool axis1Primary = Math.Abs(axis1 - 0.5) > Math.Abs(axis2 - 0.5);
      double primaryAxis = axis1Primary ? axis1 : axis2;
      if (0.3 < primaryAxis && primaryAxis < 0.7) {
        // The hit box is in the center.
        face = blockSel.Face.Opposite;
      } else {
        Vec3i normal = new();
        int primaryAxisIndex =
            ((int)blockSel.Face.Axis + (axis1Primary ? 1 : 2)) % 3;
        normal[primaryAxisIndex] = primaryAxis < 0.5 ? -1 : 1;
        face = BlockFacing.FromNormal(normal);
      }
    }
    Block oriented =
        world.BlockAccessor.GetBlock(block.CodeWithVariant("rot", face.Code));
    if (!oriented.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) {
      handling = EnumHandling.PreventDefault;
      return false;
    }
    handling = EnumHandling.PreventSubsequent;
    return oriented.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
  }
}
