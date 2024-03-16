using System;

using Vintagestory.API.Common;

namespace Lambda.CollectibleBehaviors;

// Checks the rejectAttributes attribute on item stacks when crafting. This
// requires the CollectibleObjectPatch to be installed.
public class RejectRecipeAttribute : CollectibleBehavior, ICraftingBehavior {
  public RejectRecipeAttribute(CollectibleObject collObj) : base(collObj) {}

  bool ICraftingBehavior.SatisfiesAsIngredient(
      ItemStack inputStack, GridRecipe gridRecipe,
      CraftingRecipeIngredient ingredient, ref EnumHandling handled) {
    string[] rejectAttributes =
        ingredient.RecipeAttributes?["rejectAttributes"].AsArray<string>() ??
        Array.Empty<string>();
    foreach (string attribute in rejectAttributes) {
      if (inputStack.Attributes.HasAttribute(attribute)) {
        handled = EnumHandling.PreventSubsequent;
        return false;
      }
    }
    return true;
  }
}
