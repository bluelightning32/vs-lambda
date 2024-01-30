using System;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.Network.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

// Prevents the block from being placed if it would cause a network conflict by
// connecting two sources in the same network.
public class Network : VSBlockBehavior {
  private BlockNodeTemplate _blockTemplate;

  public Network(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    NetworkSystem networkSystem = NetworkSystem.GetInstance(api);
    AutoStepManager manager = networkSystem.TokenEmitterManager;
    foreach (var beb in block.BlockEntityBehaviors) {
      if (networkSystem.NetworkBlockEntityBehaviors.ContainsKey(beb.Name)) {
        _blockTemplate = manager.ParseBlockNodeTemplate(beb.properties, 0, 0);
        break;
      }
    }
    if (_blockTemplate == null) {
      throw new ArgumentException(
          "Could not find network block entity behavior in block " +
          $"block {block.Code}.");
    }
  }

  public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    if (!_blockTemplate.CanPlace(blockSel.Position, out failureCode)) {
      handling = EnumHandling.PreventSubsequent;
      return false;
    }
    return true;
  }
}
