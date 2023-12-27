using System;
using System.Collections.Generic;

using Newtonsoft.Json.Serialization;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

// Forwards more methods from the Block to the BlockEntity.
public class BlockBehaviorNetwork : BlockBehavior {
  private BlockNodeTemplate _blockTemplate;

  public BlockBehaviorNetwork(Block block) : base(block) {}

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    IReadOnlyDictionary<string, AutoStepNetworkManager> networkManagers =
        LambdaFactoryModSystem.GetInstance(api).NetworkManagers;
    foreach (var beb in block.BlockEntityBehaviors) {
      if (networkManagers.TryGetValue(beb.Name,
                                      out AutoStepNetworkManager manager)) {
        _blockTemplate = manager.ParseBlockNodeTemplate(beb.properties);
        break;
      }
    }
    if (_blockTemplate == null) {
      throw new ArgumentException(
          "The network block behavior may only be used on a block if the network block entity behavior is also used.");
    }
  }

  public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    if (!_blockTemplate.CanPlace(blockSel.Position, ref failureCode)) {
      handling = EnumHandling.PreventSubsequent;
      return false;
    }
    return true;
  }
}
