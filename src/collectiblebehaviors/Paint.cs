using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Lambda.CollectibleBehaviors;

public class PaintProperties {
  public AssetLocation Decor;
  public float RequiresLitres = 1;
}

// Forwards the attack events to the block that the player is targeting.
public class Paint : CollectibleBehavior {
  public Paint(CollectibleObject collObj) : base(collObj) {}

  private static PaintProperties GetHoldingPaint(EntityAgent byEntity) {
    ItemStack bucketStack = byEntity.LeftHandItemSlot?.Itemstack;
    if (bucketStack?.Collectible is not ILiquidSource liquid) {
      return null;
    }
    ItemStack paint = liquid.GetContent(bucketStack);
    return paint?.ItemAttributes["paint"].AsObject<PaintProperties>();
  }

  private static bool CanPaint(EntityAgent byEntity, BlockSelection blockSel,
                               out string failureCode,
                               out PaintProperties paint,
                               out Block decorBlock) {
    paint = GetHoldingPaint(byEntity);
    decorBlock = null;
    if (paint == null) {
      failureCode = "lambda:nopaint";
      return false;
    }

    if (!byEntity.World.Claims.TryAccess((byEntity as EntityPlayer)?.Player,
                                         blockSel.Position,
                                         EnumBlockAccessFlags.BuildOrBreak)) {
      failureCode = null;
      return false;
    }

    Block parentBlock =
        byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
    decorBlock = byEntity.World.GetBlock(paint.Decor);
    if (!parentBlock.CanAttachBlockAt(byEntity.World.BlockAccessor, decorBlock,
                                      blockSel.Position, blockSel.Face)) {
      failureCode = null;
      return false;
    }
    Block existingDecor = byEntity.World.BlockAccessor.GetDecor(
        blockSel.Position, BlockSelection.GetDecorIndex(blockSel.Face));
    if (existingDecor == decorBlock) {
      failureCode = null;
      return false;
    }

    ItemStack bucketStack = byEntity.LeftHandItemSlot?.Itemstack;
    ILiquidInterface liquid = (ILiquidInterface)bucketStack.Collectible;
    if (liquid.GetCurrentLitres(bucketStack) < paint.RequiresLitres) {
      failureCode = "lambda:notenoughpaint";
      return false;
    }
    failureCode = null;
    return true;
  }

  public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
                                           BlockSelection blockSel,
                                           EntitySelection entitySel,
                                           bool firstEvent,
                                           ref EnumHandHandling handHandling,
                                           ref EnumHandling handling) {
    if (byEntity.Controls.ShiftKey || blockSel?.Position == null) {
      base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent,
                               ref handHandling, ref handling);
      return;
    }

    if (!CanPaint(byEntity, blockSel, out string failureCode,
                  out PaintProperties paint, out Block decorBlock)) {
      if (failureCode != null) {
        (byEntity.Api as ICoreClientAPI)
            ?.TriggerIngameError(this, "lambda:paint", Lang.Get(failureCode));
        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventSubsequent;
        return;
      } else {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel,
                                 firstEvent, ref handHandling, ref handling);
        return;
      }
    }
    handHandling = EnumHandHandling.PreventDefaultAction;
    handling = EnumHandling.PreventSubsequent;
    byEntity.Attributes.SetBool("startpaint", true);
    byEntity.Attributes.SetBool("didpaint", false);
  }

  public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
                                          EntityAgent byEntity,
                                          BlockSelection blockSel,
                                          EntitySelection entitySel,
                                          ref EnumHandling handling) {
    if (!byEntity.Attributes.GetBool("startpaint")) {
      return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel,
                                     entitySel, ref handling);
    }
    handling = EnumHandling.PreventSubsequent;
    if (byEntity.World.Side == EnumAppSide.Server) {
      if (byEntity.Attributes.GetBool("didpaint")) {
        return true;
      }
      if (!CanPaint(byEntity, blockSel, out string failureCode,
                    out PaintProperties paint, out Block decorBlock)) {
        handling = EnumHandling.PreventSubsequent;
        return false;
      }
      if (secondsUsed > 0.6f) {
        ItemStack decorStack = new(decorBlock);
        BlockSelection placePos = blockSel.Clone();
        // This mimics the offset logic in
        // `SystemMouseInWorldInteractions.OnBlockBuild` so that `TryPlaceBlock`
        // gets the position it expects.
        placePos.Position.Offset(blockSel.Face);
        if (!decorBlock.TryPlaceBlock(byEntity.World,
                                      (byEntity as EntityPlayer)?.Player,
                                      decorStack, placePos, ref failureCode)) {
          ((byEntity as EntityPlayer)?.Player as IServerPlayer)
              ?.SendIngameError(failureCode);
          handling = EnumHandling.PreventSubsequent;
          return false;
        }
        ILiquidSource liquid =
            (ILiquidSource)byEntity.LeftHandItemSlot.Itemstack.Collectible;
        WaterTightContainableProps waterTightProps =
            liquid.GetContentProps(byEntity.LeftHandItemSlot.Itemstack);
        liquid.TryTakeContent(
            byEntity.LeftHandItemSlot.Itemstack,
            (int)(paint.RequiresLitres * waterTightProps.ItemsPerLitre));
        byEntity.Attributes.SetBool("didpaint", true);
        byEntity.LeftHandItemSlot.MarkDirty();
      }
    }

    return secondsUsed < 1;
  }

  public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot,
                                          EntityAgent byEntity,
                                          BlockSelection blockSel,
                                          EntitySelection entitySel,
                                          ref EnumHandling handling) {
    if (!byEntity.Attributes.GetBool("startpaint")) {
      base.OnHeldAttackStop(secondsUsed, slot, byEntity, blockSel, entitySel,
                            ref handling);
      return;
    }
    handling = EnumHandling.PreventSubsequent;
    byEntity.Attributes.RemoveAttribute("startpaint");
    byEntity.Attributes.RemoveAttribute("didpaint");
  }
}
