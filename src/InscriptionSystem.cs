using System;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Lambda;

public class InscriptionSystem : ModSystem {
  private Harmony _harmony;
  private RecipeRegistryGeneric<InscriptionRecipe> _inscriptionRegistry;

  public override double ExecuteOrder() {
    // Use a value bigger than RecipeLoader's 1.0 value so that the RecipeLoader
    // initializes its api before this mod system calls it.
    return 1.1;
  }

  public static InscriptionSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<InscriptionSystem>();
  }

  public override void Start(ICoreAPI api) {
    _harmony = new Harmony(Mod.Info.ModID);
    _harmony.PatchAll();
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

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}

  public override void Dispose() {
    _harmony.UnpatchAll(Mod.Info.ModID);
    base.Dispose();
  }
}
