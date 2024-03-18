using System.Collections.Generic;

using Lambda.Blocks;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Items;

public class BlockReplacer : Item {
  private Dictionary<int, Block> _replacements;
  private float _tickDelay = 10;

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _tickDelay = Attributes["tickDelay"].AsFloat(_tickDelay);
    Dictionary<AssetLocation, AssetLocation> unresolvedReplacements =
        Attributes["replacements"]
            .AsObject<Dictionary<AssetLocation, AssetLocation>>(null,
                                                                Code.Domain);
    _replacements = new();
    foreach (KeyValuePair<AssetLocation, AssetLocation> replacement in
                 unresolvedReplacements) {
      Block source = api.World.GetBlock(replacement.Key);
      if (source == null) {
        api.Logger.Error("Unable to resolve block '{0}'", replacement.Key);
        continue;
      }
      Block target = api.World.GetBlock(replacement.Value);
      if (target == null) {
        api.Logger.Error("Unable to resolve block '{0}'", replacement.Value);
        continue;
      }
      _replacements.Add(source.Id, target);
    }
  }

  public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
                                           BlockSelection blockSel,
                                           EntitySelection entitySel,
                                           bool firstEvent,
                                           ref EnumHandHandling handling) {
    if (blockSel != null && TryReplace(byEntity, blockSel)) {
      (api.World as IClientWorldAccessor)
          ?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
      if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode !=
          EnumGameMode.Creative) {
        slot.TakeOut(1);
        slot.MarkDirty();
      }
      handling = EnumHandHandling.PreventDefault;
      return;
    }
    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent,
                             ref handling);
  }

  private bool TryReplace(EntityAgent byEntity, BlockSelection blockSel) {
    IPlayer byPlayer = byEntity as IPlayer;
    if (byPlayer != null) {
      if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position,
                                           EnumBlockAccessFlags.BuildOrBreak)) {
        return false;
      }
    }

    Block current = api.World.BlockAccessor.GetBlock(blockSel.Position);
    if (_replacements.TryGetValue(current.Id, out Block replacement)) {
      api.World.BlockAccessor.ExchangeBlock(replacement.Id, blockSel.Position);
      api.World.PlaySoundAt(
          replacement.Sounds.Place, blockSel.Position.X + 0.5f,
          blockSel.Position.Y + 0.5f, blockSel.Position.Z + 0.5f, byPlayer);
      if (_tickDelay >= 0 && api.Side == EnumAppSide.Server) {
        BlockPos positionCopy = blockSel.Position.Copy();
        api.Event.RegisterCallback(
            (delay) => SpreadingSoil.EarlyTick(api.World, positionCopy, 5),
            (int)(api.World.Rand.NextDouble() * _tickDelay * 1000));
      }
      return true;
    }

    return false;
  }
}
