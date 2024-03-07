using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

public class ConstructAdjacent : VSBlockBehavior {
  // Indexed by face
  private readonly AssetLocation[] _construct = new AssetLocation[6];
  // Indexed by face
  private readonly Block[] _resolvedConstruct = new Block[6];
  JsonItemStack _ingredient = null;

  public ConstructAdjacent(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      string constructCode = properties["construct"][face.Code]?.AsString();
      if (constructCode != null) {
        _construct[face.Index] =
            AssetLocation.Create(constructCode, CoreSystem.Domain);
      }
    }
    _ingredient = properties["ingredient"].AsObject<JsonItemStack>(
        null, CoreSystem.Domain);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    for (int i = 0; i < 6; ++i) {
      if (_construct[i] != null) {
        _resolvedConstruct[i] = api.World.GetBlock(_construct[i]);
      }
    }
    _ingredient?.Resolve(api.World, "ConstructAdjacent ", block.Code);
  }

  private bool TryConstruct(IWorldAccessor world, IPlayer byPlayer,
                            BlockSelection blockSel) {
    Block construct = _resolvedConstruct[blockSel.Face.Index];
    if (construct == null) {
      return false;
    }
    BlockPos constructPos = blockSel.Position.AddCopy(blockSel.Face);
    Block existing = world.BlockAccessor.GetBlock(constructPos);
    if (!existing.IsReplacableBy(construct)) {
      return false;
    }
    if (_ingredient != null) {
      ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

      if (!_ingredient.ResolvedItemstack.Satisfies(hotbarSlot.Itemstack)) {
        return false;
      }
      if (_ingredient.StackSize > hotbarSlot.StackSize) {
        (world.Api as ICoreClientAPI)
            ?.TriggerIngameError(this, "notenoughofingredient",
                                 Lang.Get("lambda:notenoughofingredient"));
        return false;
      }
      ItemStack removed = hotbarSlot.TakeOut(_ingredient.StackSize);
      hotbarSlot.MarkDirty();
      world.PlaySoundAt(
          construct.GetSounds(world.BlockAccessor, blockSel.Position).Place,
          constructPos.X + 0.5, constructPos.Y + 0.5, constructPos.Z + 0.5,
          byPlayer, true, 12);
    }
    world.BlockAccessor.SetBlock(construct.Id, constructPos);
    world.BlockAccessor.TriggerNeighbourBlockUpdate(constructPos);
    return true;
  }

  public override bool OnBlockInteractStart(IWorldAccessor world,
                                            IPlayer byPlayer,
                                            BlockSelection blockSel,
                                            ref EnumHandling handling) {
    if (TryConstruct(world, byPlayer, blockSel)) {
      handling = EnumHandling.PreventSubsequent;
      return true;
    } else {
      return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
    }
  }
}
