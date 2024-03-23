using System;
using System.Collections.Generic;
using System.IO;

using HarmonyLib;

using Lambda.Token;

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
  public ServerConfig ServerConfig { get; private set; }
  private Harmony _harmony;

  public static CoreSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<CoreSystem>();
  }

  private void LoadConfigFile(ICoreAPI api) {
    string configFile = $"{Domain}.json";
    if (api.Side == EnumAppSide.Server) {
      try {
        ServerConfig = api.LoadModConfig<ServerConfig>(configFile);
      } catch (Exception e) {
        api.Logger.Fatal("Error parsing '{0}': {1}", configFile, e.Message);
        throw;
      }
      if (ServerConfig == null) {
        // The file doesn't exist. So create it.
        ServerConfig = new();
        api.StoreModConfig(ServerConfig, configFile);
      }
      if (ServerConfig.CoqTmpDir == null) {
        string subpath = Path.Combine("ModData", Domain);
        ServerConfig.CoqTmpDir = api.GetOrCreateDataPath(subpath);
      }
      try {
        ServerConfig.ResolveCoqcPath();
        CoqSession session = new(ServerConfig);
        Version v = session.GetCoqcVersion();
        api.Logger.Notification("Coqc version: {0}", v);
        if (v < new Version(8, 19, 0)) {
          throw new InvalidOperationException(
              $"Coq version 8.19.0 or higher is necessary, but version {v} is installed.");
        }
      } catch (Exception e) {
        api.Logger.Fatal(e.ToString());
        ((ICoreServerAPI)api).Server.ShutDown();
      }
    }
  }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    LoadConfigFile(api);

    string patchId = Mod.Info.ModID;
    if (!Harmony.HasAnyPatches(patchId)) {
      _harmony = new Harmony(patchId);
      _harmony.PatchAll();
    }

    api.RegisterCollectibleBehaviorClass("Paint",
                                         typeof(CollectibleBehaviors.Paint));
    api.RegisterCollectibleBehaviorClass(
        "ForwardToBlock", typeof(CollectibleBehaviors.ForwardToBlock));
    api.RegisterCollectibleBehaviorClass("Term",
                                         typeof(CollectibleBehaviors.Term));
    api.RegisterCollectibleBehaviorClass(
        "RejectRecipeAttribute",
        typeof(CollectibleBehaviors.RejectRecipeAttribute));
    api.RegisterBlockClass("DestructionJig", typeof(Blocks.DestructionJig));
    api.RegisterBlockClass("SpreadingSoil", typeof(Blocks.SpreadingSoil));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviors.BlockEntityForward));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviors.BlockEntityForward));
    api.RegisterBlockBehaviorClass("Construct",
                                   typeof(BlockBehaviors.Construct));
    api.RegisterBlockBehaviorClass("DropCraft",
                                   typeof(BlockBehaviors.DropCraft));
    api.RegisterBlockBehaviorClass("MultiAttached",
                                   typeof(BlockBehaviors.MultiAttached));
    api.RegisterBlockBehaviorClass("Orient", typeof(BlockBehaviors.Orient));
    api.RegisterBlockEntityClass("FunctionContainer",
                                 typeof(BlockEntities.FunctionContainer));
    api.RegisterBlockEntityClass("ApplicationJig",
                                 typeof(BlockEntities.ApplicationJig));
    api.RegisterBlockEntityClass("DestructionJig",
                                 typeof(BlockEntities.DestructionJig));
    api.RegisterBlockEntityClass("SingleTermContainer",
                                 typeof(BlockEntities.SingleTermContainer));
    api.RegisterBlockEntityClass("TermContainer",
                                 typeof(BlockEntities.TermContainer));
    api.RegisterBlockEntityBehaviorClass(
        "CacheMesh", typeof(BlockEntityBehaviors.CacheMesh));
    api.RegisterBlockEntityBehaviorClass(
        "BlockMonitor", typeof(BlockEntityBehaviors.BlockMonitor));
    api.RegisterBlockEntityBehaviorClass(
        "SealedCharcoalPit", typeof(BlockEntityBehaviors.SealedCharcoalPit));
    api.RegisterItemClass("BlockReplacer", typeof(Items.BlockReplacer));
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}

  public override void Dispose() {
    base.Dispose();

    _harmony?.UnpatchAll(_harmony.Id);
  }
}
