using System.Collections.Generic;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Lambda;

#pragma warning disable IDE1006

[HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo))]
class CollectibleBehaviorHandbookTextAndExtraInfoPatch {
  [HarmonyPrefix]
  [HarmonyPatch("addCreatedByInfo")]
  static void addCreatedByInfoPrefix(out int __state, ICoreClientAPI capi,
                                     ItemStack[] allStacks,
                                     ActionConsumable<string> openDetailPageFor,
                                     ItemStack stack,
                                     List<RichTextComponentBase> components,
                                     float marginTop, bool haveText) {
    __state = components.Count;
  }

  [HarmonyPostfix]
  [HarmonyPatch("addCreatedByInfo")]
  static void addCreatedByInfoPostfix(
      int __state, ref bool __result, ICoreClientAPI capi,
      ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor,
      ItemStack stack, List<RichTextComponentBase> components, float marginTop,
      bool haveText) {
    bool addedCreatedBy = components.Count != __state;
    capi.Logger.Debug("addCreatedByInfoPostfix {0} {1}", __state,
                      addedCreatedBy);
  }
}

#pragma warning restore IDE1006