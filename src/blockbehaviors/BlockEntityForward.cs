using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.BlockBehaviors;

// The BlockEntity should implement this interface.
public interface IBlockEntityForward {
  public void OnNeighbourBlockChange(BlockPos neibpos,
                                     ref EnumHandling handling) {}

  public ItemStack OnPickBlock(ref EnumHandling handling) { return null; }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) { return null; }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) { return null; }

  public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel,
                                   ref EnumHandling handled) {
    // Return true and leave `handled` as is to indicate that the interaction
    // should not be stopped.
    return true;
  }
}

// Forwards more methods from the Block to the BlockEntity.
public class BlockEntityForward : StrongBlockBehavior {
  public BlockEntityForward(Block block) : base(block) {}

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handled) {
    BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
    WalkBlockEntityBehaviors(
        entity,
        (IBlockEntityForward forward, ref EnumHandling handled) =>
            forward.OnNeighbourBlockChange(neibpos, ref handled),
        ref handled);
    if (handled != EnumHandling.PassThrough) {
      return;
    }
    base.OnNeighbourBlockChange(world, pos, neibpos, ref handled);
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                                        ref EnumHandling handled) {
    ItemStack result = null;
    BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
    WalkBlockEntityBehaviors(
        entity, (IBlockEntityForward forward, ref EnumHandling handled) => {
          ItemStack localResult = forward.OnPickBlock(ref handled);
          if (handled != EnumHandling.PassThrough) {
            result = localResult;
          }
        }, ref handled);
    if (handled != EnumHandling.PassThrough) {
      return result;
    }
    return base.OnPickBlock(world, pos, ref handled);
  }

  public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor,
                                              BlockPos pos,
                                              ref EnumHandling handled) {
    List<Cuboidf> result = null;
    BlockEntity entity = blockAccessor.GetBlockEntity(pos);
    WalkBlockEntityBehaviors(
        entity, (IBlockEntityForward forward, ref EnumHandling handled) => {
          Cuboidf[] localResult = forward.GetSelectionBoxes(ref handled);
          if (handled != EnumHandling.PassThrough) {
            if (localResult != null) {
              result ??= new();
              result.AddRange(localResult);
            }
          }
        }, ref handled);
    if (handled != EnumHandling.PassThrough) {
      return result?.ToArray();
    }
    return base.GetSelectionBoxes(blockAccessor, pos, ref handled);
  }

  public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor,
                                              BlockPos pos,
                                              ref EnumHandling handled) {
    List<Cuboidf> result = null;
    BlockEntity entity = blockAccessor.GetBlockEntity(pos);
    WalkBlockEntityBehaviors(
        entity, (IBlockEntityForward forward, ref EnumHandling handled) => {
          Cuboidf[] localResult = forward.GetCollisionBoxes(ref handled);
          if (handled != EnumHandling.PassThrough) {
            if (localResult != null) {
              result ??= new();
              result.AddRange(localResult);
            }
          }
        }, ref handled);
    if (handled != EnumHandling.PassThrough) {
      return result?.ToArray();
    }
    return base.GetCollisionBoxes(blockAccessor, pos, ref handled);
  }

  public override bool OnBlockInteractStart(IWorldAccessor world,
                                            IPlayer byPlayer,
                                            BlockSelection blockSel,
                                            ref EnumHandling handled) {
    bool result = true;
    BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
    WalkBlockEntityBehaviors(
        entity, (IBlockEntityForward forward, ref EnumHandling handled) => {
          bool localResult =
              forward.OnBlockInteractStart(byPlayer, blockSel, ref handled);
          if (handled != EnumHandling.PassThrough) {
            result = localResult;
          }
        }, ref handled);
    if (handled != EnumHandling.PassThrough) {
      return result;
    }
    return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handled);
  }

  public delegate void BlockEntityBehaviorDelegate(BlockEntityBehavior behavior,
                                                   ref EnumHandling handled);
  public delegate void BlockEntityDelegate(BlockEntity entity,
                                           ref EnumHandling handled);

  public static void WalkBlockEntityBehaviors(
      BlockEntity entity, BlockEntityBehaviorDelegate callBehavior,
      BlockEntityDelegate callEntity, ref EnumHandling handled) {
    if (entity == null) {
      return;
    }
    foreach (BlockEntityBehavior behavior in entity.Behaviors) {
      EnumHandling behaviorHandled = EnumHandling.PassThrough;
      callBehavior(behavior, ref behaviorHandled);
      if (behaviorHandled != EnumHandling.PassThrough) {
        handled = behaviorHandled;
      }
      if (handled == EnumHandling.PreventSubsequent) {
        return;
      }
    }
    if (handled == EnumHandling.PreventDefault) {
      return;
    }
    EnumHandling entityHandled = EnumHandling.PassThrough;
    callEntity(entity, ref entityHandled);

    if (entityHandled != EnumHandling.PassThrough) {
      handled = entityHandled;
    }
  }

  private delegate void IBlockEntityForwardDelegate(IBlockEntityForward forward,
                                                    ref EnumHandling handled);

  private static void
  WalkBlockEntityBehaviors(BlockEntity entity,
                           IBlockEntityForwardDelegate callForward,
                           ref EnumHandling handled) {
    WalkBlockEntityBehaviors(
        entity,
        (BlockEntityBehavior behavior, ref EnumHandling handled) => {
          if (behavior is IBlockEntityForward forward) {
            callForward(forward, ref handled);
          }
        },
        (BlockEntity entity, ref EnumHandling handled) => {
          if (entity is IBlockEntityForward forward) {
            callForward(forward, ref handled);
          }
        },
        ref handled);
  }
}
