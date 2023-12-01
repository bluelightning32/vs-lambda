using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory.Tests;

[TestClass]
public class BlockEntityScopeTest {
  // This property is set by the test framework:
  // https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022#add-a-testcontext-to-the-test-class
  public TestContext TestContext { get; set; } = null!;

  [TestInitialize]
  public void Initialize() {}

  [TestMethod]
  public void ScopeCacheKeyCacheable() {
    ScopeCacheKey key1 = new ScopeCacheKey();
    key1.PortedSides = 0;
    key1.ScopeFace = -1;
    key1.Scope = Scope.Function;

    ScopeCacheKey key2 = new ScopeCacheKey();
    key2.PortedSides = 0;
    key2.ScopeFace = -1;
    key2.Scope = Scope.Function;

    Dictionary<ScopeCacheKey, int> cache = new();
    Assert.IsFalse(cache.ContainsKey(key1));
    cache[key1] = 1;
    Assert.IsTrue(cache.ContainsKey(key1));

    Assert.IsTrue(cache.ContainsKey(key2));
    Assert.AreEqual(1, cache[key2]);
  }
}