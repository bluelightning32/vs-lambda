using HarmonyLib;

using Vintagestory.API.Common;

namespace Lambda;

interface ICraftingBehavior {
  bool SatisfiesAsIngredient(ItemStack inputStack, GridRecipe gridRecipe,
                             CraftingRecipeIngredient ingredient,
                             ref EnumHandling handled);
}

#pragma warning disable IDE1006

[HarmonyPatch(typeof(CollectibleObject))]
// Changes CollectibleObject to call its behaviors in MatchesForCrafting. This
// is used for a behavior that verifies an attribute is not present.
class CollectibleObjectPatch {
  [HarmonyPrefix]
  [HarmonyPatch("MatchesForCrafting")]
  public static bool
  SatisfiesAsIngredientPrefix(ref bool __result, CollectibleObject __instance,
                              ItemStack inputStack, GridRecipe gridRecipe,
                              CraftingRecipeIngredient ingredient) {
    bool runDefault = true;
    foreach (CollectibleBehavior behavior in __instance.CollectibleBehaviors) {
      if (behavior is ICraftingBehavior craftingBehavior) {
        EnumHandling behaviorHandled = EnumHandling.PassThrough;
        bool behaviorResult = craftingBehavior.SatisfiesAsIngredient(
            inputStack, gridRecipe, ingredient, ref behaviorHandled);
        if (behaviorHandled != EnumHandling.PassThrough) {
          if (behaviorHandled == EnumHandling.PreventSubsequent) {
            // Return true to skip the default behavior.
            return false;
          }
          __result = behaviorResult;
          if (behaviorHandled == EnumHandling.PreventDefault) {
            runDefault = false;
          }
        }
      }
    }
    return runDefault;
  }
}

#pragma warning restore IDE1006
