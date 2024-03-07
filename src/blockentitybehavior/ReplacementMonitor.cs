using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.BlockEntityBehavior;

using VSBlockEntityBehavior = Vintagestory.API.Common.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

interface IReplacementWatcher {
  public void BlockReplaced(BlockPos watcher, BlockPos replaced);
}

public class ReplacementMonitor : VSBlockEntityBehavior {
  BlockPos _watcher = new(0);
  public ReplacementMonitor(VSBlockEntity blockentity) : base(blockentity) {}

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
    // For debug purposes, allow the player to create the block with:
    // /giveblock lambda:replacementblock{watcherX: x, watcherY: y, watcherZ:
    // z}.
    //
    // Do not use `GetBlockPos`, because it can't handle when the attributes
    // are internally represented as longs instead of ints, which
    // Newtonsoft.Json does by default.
    _watcher.X = byItemStack.Attributes.GetAsInt("watcherX", _watcher.X);
    _watcher.Y = byItemStack.Attributes.GetAsInt("watcherY", _watcher.Y);
    _watcher.Z = byItemStack.Attributes.GetAsInt("watcherZ", _watcher.Z);
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();

    Block watcher = Api.World.BlockAccessor.GetBlock(_watcher);
    watcher.GetInterface<IReplacementWatcher>(Api.World, _watcher)
        ?.BlockReplaced(_watcher, Pos);
  }
}
