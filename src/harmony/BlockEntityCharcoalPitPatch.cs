using HarmonyLib;

using Vintagestory.GameContent;

namespace Lambda;

interface ICharcoalConverter {
  // Returns true if the behavior handled the charcoal pit conversion.
  bool ConvertPit();
}

#pragma warning disable IDE1006

[HarmonyPatch(typeof(BlockEntityCharcoalPit))]
// Changes CollectibleObject to call its behaviors in MatchesForCrafting. This
// is used for a behavior that verifies an attribute is not present.
class BlockEntityCharcoalPitPatch {
  [HarmonyPrefix]
  [HarmonyPatch("ConvertPit")]
  public static bool ConvertPit(BlockEntityCharcoalPit __instance) {
    // If a behavior handled the conversion, then do not run the default
    // conversion.
    bool runDefault =
        !(__instance.GetBehavior<ICharcoalConverter>()?.ConvertPit() == true);
    return runDefault;
  }
}

#pragma warning restore IDE1006
