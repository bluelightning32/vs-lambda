using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class BEBehaviorNetwork : BEBehaviorAbstractNetwork {

  public static string Name {
    get { return "Network"; }
  }

  public class Manager : AutoStepNetworkManager {
    public Manager(IWorldAccessor world)
        : base(world, new NetworkNodeAccessor(
                          (pos) => world.BlockAccessor.GetBlockEntity(pos)
                                       ?.GetBehavior<BEBehaviorNetwork>())) {}

    public override
        BlockNodeTemplate ParseBlockNodeTemplate(JsonObject properties) {
      Dictionary<JsonObject, BlockNodeTemplate> cache =
          ObjectCacheUtil.GetOrCreate(
              _world.Api, $"lambdafactory-network-properties",
              () => new Dictionary<JsonObject, BlockNodeTemplate>());
      if (cache.TryGetValue(properties, out BlockNodeTemplate block)) {
        return block;
      }
      Debug("lambda: Network properties cache miss. Dict has {0} entries.",
            cache.Count);
      BlockNodeTemplateLoading loading =
          properties.AsObject<BlockNodeTemplateLoading>();
      block = new BlockNodeTemplate(loading, _accessor, this);

      cache.Add(properties, block);
      return block;
    }
  }

  public BEBehaviorNetwork(BlockEntity blockentity) : base(blockentity) {}

  protected override string GetNetworkName() { return "node"; }

  protected override AutoStepNetworkManager GetManager(ICoreAPI api) {
    return LambdaFactoryModSystem.GetInstance(api).NetworkManager;
  }
}