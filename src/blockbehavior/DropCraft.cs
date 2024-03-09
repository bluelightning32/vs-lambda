using Lambda.BlockEntityBehavior;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

public interface IDropCraftListener {
  // `pos` is the position of the listener. `dropper` is the position of the
  // DropCraft block.
  void OnDropCraft(IWorldAccessor world, BlockPos pos, BlockPos dropper);
}

public class DropCraft : VSBlockBehavior, IBlockWatcher {
  ICoreAPI _api;
  int _yieldStrength = 3;
  int _listenerSearchDist = 3;
  Block _blockMonitor;
  private BlockDropItemStack[] _yieldDrops;

  public DropCraft(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _yieldStrength = properties["yieldStrength"].AsInt(_yieldStrength);
    _listenerSearchDist =
        properties["_listenerSearchDist"].AsInt(_listenerSearchDist);
    _yieldDrops =
        properties["yieldDrops"].AsArray(block.Drops, CoreSystem.Domain);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _api = api;
    foreach (BlockDropItemStack drop in _yieldDrops) {
      drop.Resolve(api.World, "DropCraft", block.Code);
    }
    _blockMonitor = _api.World.GetBlock(
        new AssetLocation(CoreSystem.Domain, "blockmonitor"));
  }

  private void SetBlockMonitor(BlockPos pos, BlockPos monitorPos) {
    ItemStack monitorStack = new(_blockMonitor);
    monitorStack.Attributes.SetBlockPos("watcher", pos);
    _api.World.BlockAccessor.SetBlock(_blockMonitor.Id, monitorPos,
                                      monitorStack);
  }

  private bool CheckComplete(BlockPos pos) {
    if (_api.Side != EnumAppSide.Server) {
      return false;
    }
    BlockPos weightPos = pos.Copy();
    bool allMatched = true;
    for (int i = 0; i < _yieldStrength; ++i) {
      weightPos.Up();
      Block weight = _api.World.BlockAccessor.GetBlock(weightPos);
      if (weight.HasBehavior<BlockBehaviorUnstableFalling>(true)) {
        // This is a valid weight block.
        continue;
      }
      allMatched = false;
      if (weight.Id == 0) {
        // This is an air block. Replace it with a blockmonitor.
        SetBlockMonitor(pos, weightPos);
      }
    }
    // Check the extra monitor block on top
    weightPos.Up();
    Block extra = _api.World.BlockAccessor.GetBlock(weightPos);
    if (!allMatched) {
      if (extra.Id == 0) {
        SetBlockMonitor(pos, weightPos);
      }
      return false;
    }

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

    // Try to clean up the extra block monitor. This is done after this watcher
    // block is destroyed, to prevent the extra monitor from notifying this
    // watcher again.
    if (extra.Id == _blockMonitor.Id) {
      _api.World.BlockAccessor.SetBlock(0, weightPos);
    }

    _api.World.BlockAccessor.TriggerNeighbourBlockUpdate(pos);

    BlockPos listenerPos = pos.Copy();
    for (int i = 0; i < 3; ++i) {
      listenerPos.Down();
      Block listenerBlock = _api.World.BlockAccessor.GetBlock(listenerPos);
      IDropCraftListener listener =
          listenerBlock.GetInterface<IDropCraftListener>(_api.World,
                                                         listenerPos);
      if (listener != null) {
        listener.OnDropCraft(_api.World, listenerPos, pos);
        break;
      }
      if (listenerBlock.Id != 0) {
        // Some block other than air is in the way.
        break;
      }
    }

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

  public void BlockChanged(BlockPos watcher, BlockPos replaced) {
    CheckComplete(watcher);
  }
}
