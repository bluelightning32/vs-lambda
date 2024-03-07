using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
        using Process coqc = new() { StartInfo = new ProcessStartInfo {
          FileName = ServerConfig.CoqcPath, ArgumentList = { "--version" },
          UseShellExecute = false, RedirectStandardOutput = true,
          CreateNoWindow = true
        } };
        coqc.Start();
        string coqcVersion = coqc.StandardOutput.ReadToEnd();
        coqc.WaitForExit();
        if (coqc.ExitCode != 0) {
          api.Logger.Fatal("Failed to invoke '{0} --version'.",
                           ServerConfig.CoqcPath);
        }
        api.Logger.Notification("Coqc version: {0}",
                                coqcVersion.ReplaceLineEndings(" "));
      } catch (Exception e) {
        api.Logger.Fatal(e.ToString());
      }
    }
  }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    LoadConfigFile(api);

    api.RegisterCollectibleBehaviorClass("Term",
                                         typeof(CollectibleBehavior.Term));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehavior.BlockEntityForward));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehavior.BlockEntityForward));
    api.RegisterBlockBehaviorClass("DropCraft",
                                   typeof(BlockBehavior.DropCraft));
    api.RegisterBlockBehaviorClass("Orient", typeof(BlockBehavior.Orient));
    api.RegisterBlockEntityClass("FunctionContainer",
                                 typeof(BlockEntity.FunctionContainer));
    api.RegisterBlockEntityClass("TermContainer",
                                 typeof(BlockEntity.TermContainer));
    api.RegisterBlockEntityBehaviorClass("CacheMesh",
                                         typeof(BlockEntityBehavior.CacheMesh));
    api.RegisterBlockEntityBehaviorClass(
        "BlockMonitor", typeof(BlockEntityBehavior.BlockMonitor));
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}

  public override void Dispose() { base.Dispose(); }
}
