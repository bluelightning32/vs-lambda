using System;
using System.Collections.Generic;

using Lambda.BlockEntity;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

public class InventoryOptions {
  public bool RequireTerm;
  public bool RequireConstructor;
  public bool RequireFunction;
  public bool RequireInscriptionIngredient;
  public int MaxSlotStackSize = 999999;
  public string DialogTitleLangCode;
  public string DialogDescLangCode;
  public bool HidePerishRate;
  public Dictionary<string, CompositeTexture> FullTextures;

  public int GetMaxStackForItem(ICoreAPI api, ItemStack item) {
    CollectibleBehavior.Term term =
        item.Collectible.GetBehavior<CollectibleBehavior.Term>();
    if (RequireTerm) {
      if (term == null) {
        return 0;
      }
    }
    if (RequireFunction) {
      if (!(term?.IsFunction(item) ?? false)) {
        return 0;
      }
    }
    if (RequireConstructor) {
      if (term?.GetConstructs(item) == null) {
        return 0;
      }
    }
    if (RequireInscriptionIngredient) {
      int maxStack = 0;
      Dictionary<AssetLocation, List<InscriptionRecipe>> recipes =
          InscriptionSystem.GetInstance(api).GetRecipesForIngredient(item);
      foreach (List<InscriptionRecipe> group in recipes.Values) {
        foreach (InscriptionRecipe recipe in group) {
          maxStack =
              Math.Max(maxStack, recipe.Ingredient.ResolvedItemstack.StackSize);
        }
      }
      return maxStack;
    }
    return MaxSlotStackSize;
  }
}

// Controls what kind of item the container can hold. The block entity must
// inherit from BlockEntityTermContainer for this behavior to do anything.
public class Inventory : VSBlockBehavior, IInventoryControl {

  public Inventory(Block block) : base(block) {}

  private InventoryOptions _options;

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _options = properties.AsObject<InventoryOptions>();
  }

  bool IInventoryControl.GetHidePerishRate() { return _options.HidePerishRate; }

  ItemSlot IInventoryControl.GetSlot(ICoreAPI api, InventoryGeneric inventory) {
    return new SelectiveItemSlot(
        inventory, (item) => _options.GetMaxStackForItem(api, item));
  }

  string IInventoryControl.GetTitle() { return _options.DialogTitleLangCode; }

  string IInventoryControl.GetDescription() {
    return _options.DialogDescLangCode;
  }
}