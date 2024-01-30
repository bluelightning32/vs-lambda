using System;
using System.Collections.Generic;

using Vintagestory.API.Common;

namespace Lambda.Network.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

// Prevents the block from being placed if it would cause a network conflict by
// connecting two sources in the same network.
public class Network : VSBlockBehavior {
  private readonly List<BlockNodeTemplate> _blockTemplates =
      new List<BlockNodeTemplate>();

  public Network(Block block) : base(block) {}

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    IReadOnlyDictionary<string, AutoStepManager> networkManagers =
        NetworkSystem.GetInstance(api).NetworkManagers;
    foreach (var beb in block.BlockEntityBehaviors) {
      if (networkManagers.TryGetValue(beb.Name, out AutoStepManager manager)) {
        _blockTemplates.Add(
            manager.ParseBlockNodeTemplate(beb.properties, 0, 0));
        break;
      }
    }
    if (_blockTemplates.Count == 0) {
      throw new ArgumentException(
          "The network block behavior may only be used on a block if the " +
          "block also has one or more network block entity behaviors.");
    }
  }

  public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    foreach (BlockNodeTemplate template in _blockTemplates) {
      if (!template.CanPlace(blockSel.Position, out failureCode)) {
        handling = EnumHandling.PreventSubsequent;
        return false;
      }
    }
    return true;
  }
}
