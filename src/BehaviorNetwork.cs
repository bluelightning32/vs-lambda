using System;

using Newtonsoft.Json.Serialization;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

// Forwards more methods from the Block to the BlockEntity.
public class BlockBehaviorNetwork : BlockBehavior {
  private BEBehaviorNetwork.Manager _networkManager;

  private BlockNodeTemplate _blockTemplate;

  public BlockBehaviorNetwork(Block block) : base(block) {}

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _networkManager =
        api.ModLoader.GetModSystem<LambdaFactoryModSystem>().NetworkManager;
    foreach (var beb in block.BlockEntityBehaviors) {
      if (beb.Name == BEBehaviorNetwork.Name) {
        _blockTemplate =
            BEBehaviorNetwork.ParseBlockNodeTemplate(api, beb.properties);
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
    if (!_networkManager.CanPlace(_blockTemplate, blockSel.Position,
                                  ref failureCode)) {
      handling = EnumHandling.PreventSubsequent;
      return false;
    }
    return true;
  }
}
