using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;
using VSBlockEntityBehavior = Vintagestory.API.Common.BlockEntityBehavior;

// The BlockEntity should implement this interface.
public interface IBlockEntityForward {
  public void OnNeighbourBlockChange(BlockPos neibpos,
                                     ref EnumHandling handling) {}

  public ItemStack OnPickBlock(ref EnumHandling handling) { return null; }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) { return null; }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) { return null; }
}

// Forwards more methods from the Block to the BlockEntity.
public class BlockEntityForward : StrongBlockBehavior {
  public BlockEntityForward(Block block) : base(block) {}

  private IBlockEntityForward GetForward(IWorldAccessor world, BlockPos pos) {
    return GetForward(world.BlockAccessor, pos);
  }

  private IBlockEntityForward GetForward(IBlockAccessor blockAccessor,
                                         BlockPos pos) {
    if (block.EntityClass == null) {
      return null;
    }
    return blockAccessor.GetBlockEntity(pos) as IBlockEntityForward;
  }

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handling) {
    // The base sets handling to EnumHandling.PassThrough. So call the block
    // entity afterwards so that it has a chance to override it.
    base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
    VSBlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
    if (entity != null) {
      foreach (VSBlockEntityBehavior behavior in entity.Behaviors) {
        IBlockEntityForward forward = behavior as IBlockEntityForward;
        if (forward != null) {
          EnumHandling behaviorResult = EnumHandling.PassThrough;
          forward.OnNeighbourBlockChange(neibpos, ref behaviorResult);
          if (behaviorResult != EnumHandling.PassThrough) {
            handling = behaviorResult;
          }
          if (handling == EnumHandling.PreventSubsequent) {
            return;
          }
        }
      }
      if (handling == EnumHandling.PreventDefault) {
        return;
      }
      {
        IBlockEntityForward forward = entity as IBlockEntityForward;
        if (forward != null) {
          EnumHandling entityResult = EnumHandling.PassThrough;
          forward.OnNeighbourBlockChange(neibpos, ref entityResult);
          if (entityResult != EnumHandling.PassThrough) {
            handling = entityResult;
          }
          if (handling == EnumHandling.PreventSubsequent) {
            return;
          }
        }
      }
    }
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                                        ref EnumHandling handling) {
    handling = EnumHandling.PassThrough;
    return GetForward(world, pos)?.OnPickBlock(ref handling);
  }

  public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor,
                                              BlockPos pos,
                                              ref EnumHandling handled) {
    Cuboidf[] result = null;
    VSBlockEntity entity = blockAccessor.GetBlockEntity(pos);
    if (entity != null) {
      foreach (VSBlockEntityBehavior behavior in entity.Behaviors) {
        if (behavior is IBlockEntityForward forward) {
          EnumHandling behaviorHandled = EnumHandling.PassThrough;
          Cuboidf[] behaviorResult =
              forward.GetSelectionBoxes(ref behaviorHandled);
          if (behaviorHandled != EnumHandling.PassThrough) {
            handled = behaviorHandled;
            result = behaviorResult;
          }
          if (handled == EnumHandling.PreventSubsequent) {
            return result;
          }
        }
      }
      if (handled == EnumHandling.PreventDefault) {
        return result;
      }
      {
        if (entity is IBlockEntityForward forward) {
          EnumHandling entityHandled = EnumHandling.PassThrough;
          Cuboidf[] entityResult = forward.GetSelectionBoxes(ref entityHandled);
          if (entityHandled != EnumHandling.PassThrough) {
            handled = entityHandled;
            result = entityResult;
          }
          if (handled == EnumHandling.PreventSubsequent) {
            return result;
          }
        }
      }
    }
    if (handled != EnumHandling.PassThrough) {
      return result;
    }

    return base.GetSelectionBoxes(blockAccessor, pos, ref handled);
  }

  public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor,
                                              BlockPos pos,
                                              ref EnumHandling handled) {
    Cuboidf[] result = null;
    VSBlockEntity entity = blockAccessor.GetBlockEntity(pos);
    if (entity != null) {
      foreach (VSBlockEntityBehavior behavior in entity.Behaviors) {
        if (behavior is IBlockEntityForward forward) {
          EnumHandling behaviorHandled = EnumHandling.PassThrough;
          Cuboidf[] behaviorResult =
              forward.GetCollisionBoxes(ref behaviorHandled);
          if (behaviorHandled != EnumHandling.PassThrough) {
            handled = behaviorHandled;
            result = behaviorResult;
          }
          if (handled == EnumHandling.PreventSubsequent) {
            return result;
          }
        }
      }
      if (handled == EnumHandling.PreventDefault) {
        return result;
      }
      {
        if (entity is IBlockEntityForward forward) {
          EnumHandling entityHandled = EnumHandling.PassThrough;
          Cuboidf[] entityResult = forward.GetCollisionBoxes(ref entityHandled);
          if (entityHandled != EnumHandling.PassThrough) {
            handled = entityHandled;
            result = entityResult;
          }
          if (handled == EnumHandling.PreventSubsequent) {
            return result;
          }
        }
      }
    }
    if (handled != EnumHandling.PassThrough) {
      return result;
    }

    return base.GetCollisionBoxes(blockAccessor, pos, ref handled);
  }
}
