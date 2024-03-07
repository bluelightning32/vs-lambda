using Lambda.BlockEntityBehavior;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

public class DropCraft : VSBlockBehavior, IReplacementWatcher {
  ICoreAPI _api;
  int _yieldStrength = 3;
  private BlockDropItemStack[] _yieldDrops;

  public DropCraft(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _yieldStrength = properties["yieldStrength"].AsInt(_yieldStrength);
    _yieldDrops =
        properties["yieldDrops"].AsArray(block.Drops, CoreSystem.Domain);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _api = api;
    foreach (BlockDropItemStack drop in _yieldDrops) {
      drop.Resolve(api.World, "DropCraft", block.Code);
    }
  }

  private bool CheckComplete(BlockPos pos) {
    if (_api.Side != EnumAppSide.Server) {
      return false;
    }
    _api.Logger.Notification("CheckSurroundings called");
    BlockPos weightPos = pos.Copy();
    bool allMatched = true;
    for (int i = 0; i < _yieldStrength; ++i) {
      weightPos.Up();
      Block weight = _api.World.BlockAccessor.GetBlock(weightPos);
      if (weight.HasBehavior<BlockBehaviorUnstableFalling>(true)) {
        // This is a valid weight block.
        continue;
      }
      _api.Logger.Notification("Block {0} is not a match", weightPos);
      allMatched = false;
      if (weight.Id == 0) {
        _api.Logger.Notification("Placing monitor at {0}", weightPos);
        // This is an air block. Replace it with a replacementmonitor.
        Block replacement = _api.World.GetBlock(
            new AssetLocation(CoreSystem.Domain, "replacementmonitor"));
        ItemStack replacementStack = new(replacement);
        replacementStack.Attributes.SetBlockPos("watcher", pos);
        _api.World.BlockAccessor.SetBlock(replacement.Id, weightPos,
                                          replacementStack);
      }
    }
    if (!allMatched) {
      return false;
    }
    _api.Logger.Notification("All blocks matched for drop crafting at {0}",
                             pos);

    // Don't use the standard BreakBlock, so that custom drops can be created.
    foreach (BlockDropItemStack dstack in _yieldDrops) {
      ItemStack stack = dstack.GetNextItemStack();
      if (stack == null) {
        continue;
      }
      _api.World.SpawnItemEntity(
          stack, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
      if (dstack.LastDrop) {
        // This drop specifies that if the stack is non-empty, then none of the
        // subsequent drops should be generated. The stack is non-empty because
        // it is non-null.
        break;
      }
    }
    // Break the block
    _api.World.BlockAccessor.SetBlock(0, pos);
    _api.World.BlockAccessor.TriggerNeighbourBlockUpdate(pos);

    return true;
  }

  public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos,
                                     ref EnumHandling handling) {
    if (CheckComplete(pos)) {
      handling = EnumHandling.PreventSubsequent;
    }
  }

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handling) {
    if (CheckComplete(pos)) {
      handling = EnumHandling.PreventSubsequent;
    }
  }

  public void BlockReplaced(BlockPos watcher, BlockPos replaced) {
    CheckComplete(watcher);
  }
}
