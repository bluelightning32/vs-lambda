using System.Collections.Generic;

using Cairo;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Lambda;

#pragma warning disable IDE1006

[HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo))]
[HarmonyPatchCategory(nameof(InscriptionSystem))]
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

  private static ClearFloatTextComponent Newline(ICoreClientAPI capi,
                                                 float height) {
    return new ClearFloatTextComponent(capi, height);
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
    haveText = __result;
    List<RichTextComponentBase> inscriptionComponents =
        InscriptionSystem.GetInstance(capi).GetHandbookCreatedByInfo(
            allStacks, openDetailPageFor, stack, marginTop);
    if ((inscriptionComponents?.Count ?? 0) != 0) {
      if (!addedCreatedBy) {
        // If the page already has elements, then add a new line.
        if (haveText) {
          components.Add(Newline(capi, marginTop));
        }
        CairoFont boldFont =
            CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold);
        components.Add(new RichTextComponent(
            capi, Lang.Get("Created by") + "\n", boldFont));
        addedCreatedBy = true;
      }
      components.AddRange(inscriptionComponents);
      haveText = true;
    }
    __result = haveText;
  }

  [HarmonyPostfix]
  [HarmonyPatch("addProcessesIntoInfo")]
  static void addProcessesIntoInfoPostfix(
      ref bool __result, ItemSlot inSlot, ICoreClientAPI capi,
      ActionConsumable<string> openDetailPageFor, ItemStack stack,
      List<RichTextComponentBase> components, float marginTop,
      float marginBottom, bool haveText) {
    __result = InscriptionSystem.GetInstance(capi).AddHandbookProcessesIntoInfo(
        openDetailPageFor, stack, components, marginTop, marginBottom,
        __result);
  }
}

#pragma warning restore IDE1006