using Lambda.BlockBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.BlockEntityBehaviors;

interface IBlockWatcher {
  public void BlockChanged(BlockPos watcher, BlockPos changed);
}

// Monitors for block replacement.
public class BlockMonitor : BlockEntityBehavior, IBlockEntityForward {
  BlockPos _watcher = new(0);
  public BlockMonitor(BlockEntity blockentity) : base(blockentity) {}

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);

    _watcher = tree.GetBlockPos("watcher", _watcher);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);

    tree.SetBlockPos("watcher", _watcher);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    // `byItemStack` is null on the client side when it was created by an item
    // stack on the server side.
    if (byItemStack != null) {
      // Do not use `GetBlockPos`, because it can't handle when the attributes
      // are internally represented as longs instead of ints, which
      // happens when the block is created with the giveblock command.
      _watcher.X = byItemStack.Attributes.GetAsInt("watcherX", _watcher.X);
      _watcher.Y = byItemStack.Attributes.GetAsInt("watcherY", _watcher.Y);
      _watcher.Z = byItemStack.Attributes.GetAsInt("watcherZ", _watcher.Z);
    }
  }

  public void NotifyWatcher(BlockPos changed) {
    Block watcher = Api.World.BlockAccessor.GetBlock(_watcher);
    watcher.GetInterface<IBlockWatcher>(Api.World, _watcher)
        ?.BlockChanged(_watcher, changed);
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();

    NotifyWatcher(Pos);
  }

  void IBlockEntityForward.OnNeighbourBlockChange(BlockPos neibpos,
                                                  ref EnumHandling handling) {
    NotifyWatcher(neibpos);
  }
}
