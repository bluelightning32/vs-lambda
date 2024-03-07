using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

// Rightclicking on the source block with the correct ingredient constructs a
// new block.
public class Construct : VSBlockBehavior {
  // Indexed by face
  private readonly AssetLocation[] _construct = new AssetLocation[6];
  CraftingRecipeIngredient _ingredient = null;
  bool _adjacent = true;

  public Construct(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _adjacent = properties["adjacent"].AsBool(_adjacent);
    string constructCode = properties["construct"]["all"]?.AsString();
    if (constructCode != null) {
      foreach (BlockFacing face in BlockFacing.ALLFACES) {
        _construct[face.Index] =
            AssetLocation.Create(constructCode, CoreSystem.Domain);
      }
    }
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      constructCode = properties["construct"][face.Code]?.AsString();
      if (constructCode != null) {
        _construct[face.Index] =
            AssetLocation.Create(constructCode, CoreSystem.Domain);
      }
    }
    _ingredient = properties["ingredient"].AsObject<CraftingRecipeIngredient>(
        null, CoreSystem.Domain);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    _ingredient?.Resolve(api.World, "Construct");
  }

  private bool TryConstruct(IWorldAccessor world, IPlayer byPlayer,
                            BlockSelection blockSel) {
    AssetLocation construct = _construct[blockSel.Face.Index];
    if (construct == null) {
      return false;
    }

    BlockPos constructPos = blockSel.Position;
    if (_adjacent) {
      constructPos = blockSel.Position.AddCopy(blockSel.Face);
    }
    Block constructBlock;
    ItemSlot hotbarSlot = null;
    if (_ingredient != null) {
      hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

      if (!_ingredient.SatisfiesAsIngredient(hotbarSlot.Itemstack, false)) {
        return false;
      }
      if (_ingredient.IsWildCard) {
        string codepart = WildcardUtil.GetWildcardValue(
            _ingredient.Code, hotbarSlot.Itemstack.Collectible.Code);
        construct = RegistryObject.FillPlaceHolder(
            construct.Clone(),
            new OrderedDictionary<string, string>() { { "input", codepart } });
      }
    }
    constructBlock = world.GetBlock(construct);
    if (constructBlock == null) {
      return false;
    }
    if (_adjacent) {
      Block existing = world.BlockAccessor.GetBlock(constructPos);
      if (!existing.IsReplacableBy(constructBlock)) {
        return false;
      }
    }

    if (_ingredient != null) {
      if (_ingredient.Quantity > hotbarSlot.StackSize) {
        (world.Api as ICoreClientAPI)
            ?.TriggerIngameError(this, "notenoughofingredient",
                                 Lang.Get("lambda:notenoughofingredient"));
        return false;
      }
      if (!hotbarSlot.Itemstack.Collectible.MatchesForCrafting(
              hotbarSlot.Itemstack, null, _ingredient)) {
        return false;
      }
      hotbarSlot.Itemstack.Collectible.OnConsumedByCrafting(
          null, hotbarSlot, null, _ingredient, byPlayer, _ingredient.Quantity);
      hotbarSlot.MarkDirty();
    }

    // SetBlock internally calls OnBlockPlaced.
    world.BlockAccessor.SetBlock(constructBlock.Id, constructPos);
    world.PlaySoundAt(
        constructBlock.GetSounds(world.BlockAccessor, blockSel.Position).Place,
        constructPos.X + 0.5, constructPos.Y + 0.5, constructPos.Z + 0.5,
        byPlayer, true, 12);

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
