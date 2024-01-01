using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace LambdaFactory.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public class ScopeNetwork : AbstractNetwork {

  public static string Name {
    get { return "ScopeNetwork"; }
  }

  public class Manager : AutoStepNetworkManager {
    public Manager(IWorldAccessor world)
        : base(world, new NetworkNodeAccessor(
                          (pos) => world.BlockAccessor.GetBlockEntity(pos)
                                       ?.GetBehavior<ScopeNetwork>())) {}

    public override
        BlockNodeTemplate ParseBlockNodeTemplate(JsonObject properties) {
      Dictionary<JsonObject, BlockNodeTemplate> cache =
          ObjectCacheUtil.GetOrCreate(
              _world.Api, $"lambdafactory-scope-network-properties",
              () => new Dictionary<JsonObject, BlockNodeTemplate>());
      if (cache.TryGetValue(properties, out BlockNodeTemplate block)) {
        return block;
      }
      Debug(
          "lambda: Scope network properties cache miss. Dict has {0} entries.",
          cache.Count);
      NodeTemplate[] nodeTemplates =
          properties["nodes"]?.AsObject<NodeTemplate[]>();
      block = new BlockNodeTemplate(_accessor, this, nodeTemplates);

      cache.Add(properties, block);
      return block;
    }

    public override string GetNetworkName() { return "scope"; }
  }

  public ScopeNetwork(VSBlockEntity blockentity) : base(blockentity) {}

  protected override AutoStepNetworkManager GetManager(ICoreAPI api) {
    return LambdaFactoryModSystem.GetInstance(api).ScopeNetworkManager;
  }
}