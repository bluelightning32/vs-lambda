using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

// Rightclicking on the source block with the correct ingredient constructs a
// new block.
public class MultiAttached : VSBlockBehavior {
  // Indexed by face
  private readonly AssetLocation[] _requiredSides = new AssetLocation[6];

  public MultiAttached(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      string attachment = properties["requiredSides"][face.Code]?.AsString();
      if (attachment != null) {
        _requiredSides[face.Index] =
            AssetLocation.Create(attachment, CoreSystem.Domain);
      }
    }
  }

  public bool CheckAttachment(IWorldAccessor world, BlockPos pos) {
    BlockPos attachPos = new(0);
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      AssetLocation attachCode = _requiredSides[face.Index];
      if (attachCode == null) {
        continue;
      }

      attachPos.Set(pos).Add(face);
      Block attach = world.BlockAccessor.GetBlock(attachPos);
      if (!WildcardUtil.Match(attachCode, attach.Code)) {
        // The block is not fully attached. So destroy it. Shouldn't call
        // BreakBlock, because the player is unknown.
        ItemStack[] drops = block.GetDrops(world, pos, null);
        foreach (ItemStack drop in drops) {
          world.SpawnItemEntity(
              drop, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
        }
        world.BlockAccessor.SetBlock(0, pos);
        return false;
      }
    }
    return true;
  }

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handling) {
    if (!CheckAttachment(world, pos)) {
      handling = EnumHandling.PreventSubsequent;
    } else {
      base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
    }
  }

  public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos,
                                     ref EnumHandling handling) {
    if (!CheckAttachment(world, blockPos)) {
      handling = EnumHandling.PreventSubsequent;
    } else {
      base.OnBlockPlaced(world, blockPos, ref handling);
    }
  }
}
