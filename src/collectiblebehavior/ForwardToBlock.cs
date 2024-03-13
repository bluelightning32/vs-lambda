using Lambda.BlockBehavior;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.CollectibleBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;
using VSBlockEntityBehavior = Vintagestory.API.Common.BlockEntityBehavior;
using VSCollectibleBehavior = Vintagestory.API.Common.CollectibleBehavior;

public interface ICollectibleTarget {
  public void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity,
                                BlockSelection blockSel,
                                EntitySelection entitySel,
                                ref EnumHandHandling handHandling,
                                ref EnumHandling handled) {}

  public bool OnHeldAttackStep(BlockPos originalTarget, float secondsPassed,
                               ItemSlot slot, EntityAgent byEntity,
                               BlockSelection blockSelection,
                               EntitySelection entitySel,
                               ref EnumHandling handled) {
    return true;
  }

  public bool OnHeldAttackCancel(BlockPos originalTarget, float secondsPassed,
                                 ItemSlot slot, EntityAgent byEntity,
                                 BlockSelection blockSelection,
                                 EntitySelection entitySel,
                                 EnumItemUseCancelReason cancelReason,
                                 ref EnumHandling handled) {
    return true;
  }
}

// Forwards the attack events to the block that the player is targeting.
public class ForwardToBlock : VSCollectibleBehavior {
  protected ICoreAPI _api;

  public ForwardToBlock(CollectibleObject collObj) : base(collObj) {}

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _api = api;
  }

  public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity,
                                         BlockSelection blockSel,
                                         EntitySelection entitySel,
                                         ref EnumHandHandling handHandling,
                                         ref EnumHandling handled) {
    if (blockSel != null) {
      Block targetBlock = blockSel.Block;
      if (targetBlock == null && _api.Side == EnumAppSide.Server) {
        // `blockSel.Block` isn't filled in on the server.
        targetBlock = _api.World.BlockAccessor.GetBlock(blockSel.Position);
      }
      EnumHandHandling localHandHandling = EnumHandHandling.NotHandled;
      WalkBlockBehaviors(
          _api.World.BlockAccessor, targetBlock, blockSel.Position,
          (ICollectibleTarget forward, ref EnumHandling handled) => {
            forward.OnHeldAttackStart(slot, byEntity, blockSel, entitySel,
                                      ref localHandHandling, ref handled);
          },
          ref handled);
      if (handled != EnumHandling.PassThrough) {
        handHandling = localHandHandling;
        if (localHandHandling != EnumHandHandling.NotHandled) {
          slot.Itemstack.TempAttributes.SetBlockPos("forward-target",
                                                    blockSel.Position);
        } else {
          ClearForwardTarget(slot);
        }
        return;
      }
    }
    ClearForwardTarget(slot);
    base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel,
                           ref handHandling, ref handled);
  }

  private static void ClearForwardTarget(ItemSlot slot) {
    slot.Itemstack.TempAttributes.RemoveAttribute("forward-targetX");
    slot.Itemstack.TempAttributes.RemoveAttribute("forward-targetY");
    slot.Itemstack.TempAttributes.RemoveAttribute("forward-targetZ");
  }

  public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot,
                                        EntityAgent byEntity,
                                        BlockSelection blockSelection,
                                        EntitySelection entitySel,
                                        ref EnumHandling handled) {
    BlockPos target =
        slot.Itemstack.TempAttributes.GetBlockPos("forward-target");
    if (target != null) {
      Block targetBlock = _api.World.BlockAccessor.GetBlock(target);
      bool result = false;
      WalkBlockBehaviors(
          _api.World.BlockAccessor, targetBlock,
          target, (ICollectibleTarget forward, ref EnumHandling handled) => {
            bool localResult = forward.OnHeldAttackStep(
                target, secondsPassed, slot, byEntity, blockSelection,
                entitySel, ref handled);
            if (handled != EnumHandling.PassThrough) {
              result = localResult;
            }
          }, ref handled);
      if (handled != EnumHandling.PassThrough) {
        return result;
      }
    }
    return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection,
                                 entitySel, ref handled);
  }

  public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot,
                                          EntityAgent byEntity,
                                          BlockSelection blockSelection,
                                          EntitySelection entitySel,
                                          EnumItemUseCancelReason cancelReason,
                                          ref EnumHandling handled) {
    BlockPos target =
        slot.Itemstack.TempAttributes.GetBlockPos("forward-target");
    if (target != null) {
      Block targetBlock = _api.World.BlockAccessor.GetBlock(target);
      bool result = false;
      WalkBlockBehaviors(
          _api.World.BlockAccessor, targetBlock,
          target, (ICollectibleTarget forward, ref EnumHandling handled) => {
            bool localResult = forward.OnHeldAttackCancel(
                target, secondsPassed, slot, byEntity, blockSelection,
                entitySel, cancelReason, ref handled);
            if (handled != EnumHandling.PassThrough) {
              result = localResult;
            }
          }, ref handled);
      if (handled != EnumHandling.PassThrough) {
        return result;
      }
    }
    return base.OnHeldAttackCancel(secondsPassed, slot, byEntity,
                                   blockSelection, entitySel, cancelReason,
                                   ref handled);
  }

  public delegate void BlockDelegate(Block block, ref EnumHandling handled);

  public delegate void BlockBehaviorDelegate(VSBlockBehavior behavior,
                                             ref EnumHandling handled);
  public static void
  WalkBlockBehaviors(IBlockAccessor blockAccessor, Block block, BlockPos pos,
                     BlockBehaviorDelegate callBehavior,
                     BlockDelegate callBlock,
                     BlockEntityForward.BlockEntityDelegate callEntity,
                     ref EnumHandling handled) {
    foreach (VSBlockBehavior behavior in block.BlockBehaviors) {
      EnumHandling behaviorHandled = EnumHandling.PassThrough;
      callBehavior(behavior, ref behaviorHandled);
      if (behaviorHandled != EnumHandling.PassThrough) {
        handled = behaviorHandled;
      }
      if (handled == EnumHandling.PreventSubsequent) {
        return;
      }
    }
    if (handled != EnumHandling.PreventDefault) {
      EnumHandling blockHandled = EnumHandling.PassThrough;
      callBlock(block, ref blockHandled);

      if (blockHandled != EnumHandling.PassThrough) {
        handled = blockHandled;
      }
      if (handled == EnumHandling.PreventSubsequent) {
        return;
      }
    }

    {
      VSBlockEntity entity = blockAccessor.GetBlockEntity(pos);
      if (entity == null) {
        return;
      }
      EnumHandling blockEntityHandled = EnumHandling.PassThrough;
      callEntity(entity, ref blockEntityHandled);
      if (blockEntityHandled != EnumHandling.PassThrough) {
        handled = blockEntityHandled;
      }
    }
  }

  public static void WalkBlockBehaviors(
      IBlockAccessor blockAccessor, Block block, BlockPos pos,
      BlockBehaviorDelegate callBehavior, BlockDelegate callBlock,
      BlockEntityForward.BlockEntityBehaviorDelegate callEntityBehavior,
      BlockEntityForward.BlockEntityDelegate callEntity,
      ref EnumHandling handled) {
    WalkBlockBehaviors(
        blockAccessor, block, pos, callBehavior, callBlock,
        (VSBlockEntity entity, ref EnumHandling handled) =>
            BlockEntityForward.WalkBlockEntityBehaviors(
                entity, callEntityBehavior, callEntity, ref handled),
        ref handled);
  }

  private delegate void ICollectibleTargetDelegate(ICollectibleTarget forward,
                                                   ref EnumHandling handled);
  private static void WalkBlockBehaviors(IBlockAccessor blockAccessor,
                                         Block block, BlockPos pos,
                                         ICollectibleTargetDelegate callForward,
                                         ref EnumHandling handled) {
    WalkBlockBehaviors(
        blockAccessor, block, pos,
        (VSBlockBehavior behavior, ref EnumHandling handled) => {
          if (behavior is ICollectibleTarget forward) {
            callForward(forward, ref handled);
          }
        },
        (Block block, ref EnumHandling handled) => {
          if (block is ICollectibleTarget forward) {
            callForward(forward, ref handled);
          }
        },
        (VSBlockEntityBehavior behavior, ref EnumHandling handled) => {
          if (behavior is ICollectibleTarget forward) {
            callForward(forward, ref handled);
          }
        },
        (VSBlockEntity entity, ref EnumHandling handled) => {
          if (entity is ICollectibleTarget forward) {
            callForward(forward, ref handled);
          }
        },
        ref handled);
  }
}
