using System;
using System.Collections.Generic;
using System.Linq;

using Cairo;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Lambda;

public class InscriptionSystem : ModSystem {
  private RecipeRegistryGeneric<InscriptionRecipe> _inscriptionRegistry;
  private ICoreAPI _api;
  private Harmony _harmony;

  public override double ExecuteOrder() {
    // Use a value bigger than RecipeLoader's 1.0 value so that the RecipeLoader
    // initializes its api before this mod system calls it.
    return 1.1;
  }

  public static InscriptionSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<InscriptionSystem>();
  }

  public override void Start(ICoreAPI api) {
    string patchId = $"{Mod.Info.ModID}.{nameof(InscriptionSystem)}";
    if (!Harmony.HasAnyPatches(patchId)) {
      _harmony = new Harmony(patchId);
      _harmony.PatchCategory(nameof(InscriptionSystem));
    }
    _api = api;
    _inscriptionRegistry =
        api.RegisterRecipeRegistry<RecipeRegistryGeneric<InscriptionRecipe>>(
            "inscriptionrecipes");
  }

  public override void AssetsLoaded(ICoreAPI api) {
    base.AssetsLoaded(api);
    if (api is not ICoreServerAPI) {
      return;
    }
    RecipeLoader loader = api.ModLoader.GetModSystem<RecipeLoader>();
    loader.LoadRecipes<InscriptionRecipe>(
        "inscription recipe", "recipes/inscription", RegisterInscriptionRecipe);
  }

  private void RegisterInscriptionRecipe(InscriptionRecipe recipe) {
    if (!RecipeRegistrySystem.canRegister) {
      throw new InvalidOperationException(
          "Too late to register new inscription recipes.");
    }
    recipe.RecipeId = _inscriptionRegistry.Recipes.Count + 1;
    _inscriptionRegistry.Recipes.Add(recipe);
  }

  public List<RichTextComponentBase>
  GetHandbookCreatedByInfo(ItemStack[] allStacks,
                           ActionConsumable<string> openDetailPageFor,
                           ItemStack stack, float marginTop) {
    ICoreClientAPI capi = (ICoreClientAPI)_api;
    List<RichTextComponentBase> components = new();
    Dictionary<AssetLocation, List<InscriptionRecipe>> groupedRecipes =
        GetRecipesForOutput(stack);
    if (groupedRecipes.Count == 0) {
      return components;
    }
    components.Add(Newline(marginTop));
    components.Add(new RichTextComponent(capi,
                                         "• " + Lang.Get("Inscribing") + "\n",
                                         CairoFont.WhiteSmallText()));
    components.AddRange(GetRecipeComponents(groupedRecipes, openDetailPageFor));
    components.Add(Newline(10));
    return components;
  }

  // Returns a list of GUI components that display `groupedRecipes`.
  private List<RichTextComponentBase> GetRecipeComponents(
      Dictionary<AssetLocation, List<InscriptionRecipe>> groupedRecipes,
      ActionConsumable<string> openDetailPageFor) {
    ICoreClientAPI capi = (ICoreClientAPI)_api;
    List<RichTextComponentBase> components = new();
    bool first = true;
    foreach (List<InscriptionRecipe> group in groupedRecipes.Values) {
      if (!first) {
        components.Add(Newline(10));
      }
      first = false;
      SlideshowItemstackTextComponent ingredient =
          new(capi, group.Select(r => r.Ingredient.ResolvedItemstack).ToArray(),
              GuiStyle.LargeFontSize, EnumFloat.Inline,
              (ingredient) =>
                  openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(
                      ingredient))) { ShowStackSize = true };
      components.Add(ingredient);
      RichTextComponent text =
          new(capi, " => (" + group.First().PuzzleType + ") => ",
              CairoFont.WhiteSmallText()) { VerticalAlign =
                                                EnumVerticalAlign.Middle };
      components.Add(text);
      SlideshowItemstackTextComponent output =
          new(capi, group.Select(r => r.Output.ResolvedItemstack).ToArray(),
              GuiStyle.LargeFontSize, EnumFloat.Inline,
              (output) =>
                  openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(
                      output))) { ShowStackSize = true };
      components.Add(output);
    }
    return components;
  }

  private ClearFloatTextComponent Newline(float height) {
    return new ClearFloatTextComponent((ICoreClientAPI)_api, height);
  }

  // Returns all of the recipes that produce the output. The recipes are grouped
  // by their name.
  private Dictionary<AssetLocation, List<InscriptionRecipe>>
  GetRecipesForOutput(ItemStack output) {
    Dictionary<AssetLocation, List<InscriptionRecipe>> result = new();
    foreach (InscriptionRecipe recipe in _inscriptionRegistry.Recipes) {
      if (recipe.Output.ResolvedItemstack.Equals(
              _api.World, output, GlobalConstants.IgnoredStackAttributes)) {
        if (!result.TryGetValue(recipe.Name,
                                out List<InscriptionRecipe> resultList)) {
          resultList = new();
          result.Add(recipe.Name, resultList);
        }
        resultList.Add(recipe);
      }
    }
    return result;
  }

  public bool AddHandbookProcessesIntoInfo(
      ActionConsumable<string> openDetailPageFor, ItemStack stack,
      List<RichTextComponentBase> components, float marginTop,
      float marginBottom, bool haveText) {
    ICoreClientAPI capi = (ICoreClientAPI)_api;
    Dictionary<AssetLocation, List<InscriptionRecipe>> groupedRecipes =
        GetRecipesForIngredient(stack);
    if (groupedRecipes.Count == 0) {
      return haveText;
    }
    if (haveText) {
      components.Add(Newline(marginTop));
    }
    components.Add(new RichTextComponent(
        capi, Lang.Get("Accepts inscriptions") + "\n",
        CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
    components.AddRange(GetRecipeComponents(groupedRecipes, openDetailPageFor));
    components.Add(Newline(marginBottom));
    return haveText;
  }

  // Returns all of the recipes that take the ingredient, ignoring stack
  // attributes. The recipes are grouped by their name.
  public Dictionary<AssetLocation, List<InscriptionRecipe>>
  GetRecipesForIngredient(ItemStack input) {
    Dictionary<AssetLocation, List<InscriptionRecipe>> result = new();
    foreach (InscriptionRecipe recipe in _inscriptionRegistry.Recipes) {
      if (recipe.Ingredient.ResolvedItemstack.Equals(
              _api.World, input, GlobalConstants.IgnoredStackAttributes)) {
        if (!result.TryGetValue(recipe.Name,
                                out List<InscriptionRecipe> resultList)) {
          resultList = new();
          result.Add(recipe.Name, resultList);
        }
        resultList.Add(recipe);
      }
    }
    return result;
  }

  // Get the canonical recipe for the ingredient, or null if there is no recipe
  public InscriptionRecipe GetRecipeForIngredient(ItemStack input) {
    if (input == null) {
      return null;
    }
    Dictionary<AssetLocation, List<InscriptionRecipe>> recipes =
        GetRecipesForIngredient(input);
    foreach (var group in recipes.Values) {
      foreach (InscriptionRecipe recipe in group) {
        if (recipe.Ingredient.ResolvedItemstack.StackSize == input.StackSize) {
          return recipe;
        }
      }
    }
    return null;
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}

  public override void Dispose() {
    base.Dispose();

    _harmony?.UnpatchAll(_harmony.Id);
  }
}
