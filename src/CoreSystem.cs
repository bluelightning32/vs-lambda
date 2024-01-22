using System;
using System.Collections.Generic;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Lambda;

public static class ICoreAPIExtension {
  public static T GetCachedModSystem<T>(this ICoreAPI api)
      where T : ModSystem {
    Dictionary<Type, ModSystem> systemsCache =
        ObjectCacheUtil.GetOrCreate<Dictionary<Type, ModSystem>>(
            api, "mods-by-type", () => new());
    if (systemsCache.TryGetValue(typeof(T), out ModSystem system)) {
      return (T)system;
    }
    T t = api.ModLoader.GetModSystem<T>();
    if (t != null) {
      systemsCache.Add(typeof(T), t);
    }
    return t;
  }
}

public class CoreSystem : ModSystem {
  public static string Domain { get; private set; }
  private Harmony _harmony;

  public static CoreSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<CoreSystem>();
  }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    _harmony = new Harmony(Domain);
    _harmony.PatchAll();
    api.RegisterCollectibleBehaviorClass("Term",
                                         typeof(CollectibleBehavior.Term));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehavior.BlockEntityForward));
    api.RegisterBlockBehaviorClass("Inventory",
                                   typeof(BlockBehavior.Inventory));
    api.RegisterBlockBehaviorClass("Orient", typeof(BlockBehavior.Orient));
    api.RegisterBlockEntityClass("FunctionContainer",
                                 typeof(BlockEntity.FunctionContainer));
    api.RegisterBlockEntityClass("TermContainer",
                                 typeof(BlockEntity.TermContainer));
    api.RegisterBlockEntityBehaviorClass("CacheMesh",
                                         typeof(BlockEntityBehavior.CacheMesh));
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}

  public override void Dispose() {
    _harmony.UnpatchAll(Domain);
    base.Dispose();
  }
}
