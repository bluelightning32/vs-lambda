using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.IO;

namespace LambdaFactory.Tests;

// This class allows the Vintagestory dlls to be loaded from $(VINTAGE_STORY).
[TestClass]
public class AssemblyInitializer {
  static ResolveEventHandler _assemblyResolveDelegate = null;

  [AssemblyInitialize()]
  public static void Setup(TestContext testContext) {
    _assemblyResolveDelegate = new ResolveEventHandler(
        (sender, args) => LoadFromVintageStory(testContext, sender, args));
    AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveDelegate;
  }

  [AssemblyCleanup]
  public static void TearDown() {
    AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolveDelegate;
  }

  static Assembly LoadFromVintageStory(TestContext testContext, object sender,
                                       ResolveEventArgs args) {
    string vsDir = Environment.GetEnvironmentVariable("VINTAGE_STORY");
    if (vsDir == null) {
      testContext.WriteLine(
          "Warning: the VINTAGE_STORY environmental variable is unset. The tests will likely be unable to load the Vintagestory dlls.");
      return null;
    }
    string assemblyFile = Path.Combine(
        vsDir, Path.ChangeExtension(new AssemblyName(args.Name).Name, ".dll"));
    if (!File.Exists(assemblyFile)) {
      return null;
    }
    return Assembly.LoadFrom(assemblyFile);
  }
}