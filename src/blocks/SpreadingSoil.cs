using System;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.Blocks;

public class SpreadingSoil : BlockSoil {
  int _afterSpreadBlock;
  private Dictionary<int, int> _spreadBlocks;
  private float _spreadChance = 0.8f;
  private int _spreadDistance = 1;
  private float _spreadDelay = 10;

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _spreadChance = Attributes["spreadChance"].AsFloat(_spreadChance);
    _spreadDistance = Attributes["spreadDistance"].AsInt(_spreadDistance);
    _spreadDelay = Attributes["spreadDelay"].AsFloat(_spreadDelay);
    _afterSpreadBlock =
        api.World
            .GetBlock(AssetLocation.Create(
                Attributes["afterSpreadBlock"].AsString(), Code.Domain))
            .Id;
    Dictionary<AssetLocation, AssetLocation> unresolvedSpreadBlocks =
        Attributes["spreadBlocks"]
            .AsObject<Dictionary<AssetLocation, AssetLocation>>(null,
                                                                Code.Domain);
    _spreadBlocks = new();
    foreach (KeyValuePair<AssetLocation, AssetLocation> spread in
                 unresolvedSpreadBlocks) {
      Block source = api.World.GetBlock(spread.Key);
      if (source == null) {
        api.Logger.Error("Unable to resolve block '{0}'", spread.Key);
        continue;
      }
      Block target = api.World.GetBlock(spread.Value);
      if (target == null) {
        api.Logger.Error("Unable to resolve block '{0}'", spread.Value);
        continue;
      }
      _spreadBlocks.Add(source.Id, target.Id);
    }
  }

  public override bool ShouldReceiveServerGameTicks(IWorldAccessor world,
                                                    BlockPos pos,
                                                    Random offThreadRandom,
                                                    out object extra) {
    if (offThreadRandom.NextDouble() <= _spreadChance) {
      // This tick should spread the block
      extra = null;
      return true;
    }
    // This tick should not spread the block. Maybe the base class wants to tick
    // the block anyway to grow grass.
    return base.ShouldReceiveServerGameTicks(world, pos, offThreadRandom,
                                             out extra);
  }

  public override void OnServerGameTick(IWorldAccessor world, BlockPos pos,
                                        object extra) {
    if (extra != null) {
      // This is not a spread tick. The base class will use the tick to grow
      // grass.
      base.OnServerGameTick(world, pos, extra);
    }
    world.Logger.Notification("Block conversion running at {0}",
                              pos.ToLocalPosition(api));
    // This is a spread tick. Don't call the base class, because it will crash
    // on the `extra` value.
    for (int x = -_spreadDistance; x <= _spreadDistance; ++x) {
      for (int y = -_spreadDistance; y <= _spreadDistance; ++y) {
        for (int z = -_spreadDistance; z <= _spreadDistance; ++z) {
          BlockPos spreadPos =
              new(pos.X + x, pos.Y + y, pos.Z + z, pos.dimension);
          if (spreadPos == pos) {
            continue;
          }
          int neighbor = world.BlockAccessor.GetBlock(spreadPos).BlockId;
          if (_spreadBlocks.TryGetValue(neighbor, out int replacement)) {
            world.BlockAccessor.ExchangeBlock(replacement, spreadPos);
            world.Api.Event.RegisterCallback(
                (delay) => EarlyTick(world, spreadPos),
                (int)(world.Rand.NextDouble() * _spreadDelay * 1000));
          }
        }
      }
    }
    // Now that the spreading is done, replace this block with its final block.
    world.BlockAccessor.ExchangeBlock(_afterSpreadBlock, pos);
  }

  public static void EarlyTick(IWorldAccessor world, BlockPos pos,
                               int attempts = 1) {
    Block found = world.BlockAccessor.GetBlock(pos);
    for (int i = 0; i < attempts; ++i) {
      if (found.ShouldReceiveServerGameTicks(world, pos, world.Rand,
                                             out object extra)) {
        found.OnServerGameTick(world, pos, extra);
        return;
      }
    }
  }
}
