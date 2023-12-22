using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory.Tests;

[TestClass]
public class BlockNodeTemplateTest {
  private NetworkManager _manager;
  private MemoryNodeAccessor _accessor;

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new NetworkManager(_accessor);
  }

  [TestMethod]
  public void ParseSource() {
    BlockNodeTemplate template = TestBlockNodeTemplates.ScopeCenterSource;
    Assert.AreEqual(1, template.Count);
    NodeTemplate nodeTemplate =
        TestBlockNodeTemplates.ScopeCenterSource.GetNodeTemplate(
            true, Edge.NorthCenter);
    Assert.IsNotNull(nodeTemplate);
    Assert.IsTrue(nodeTemplate.Source);
  }

  [TestMethod]
  public void CanPlaceNextToSource() {
    _accessor.SetBlock(0, 0, 0, 0, TestBlockNodeTemplates.ScopeCenterSource);
    string failureCode = null;
    Assert.IsTrue(TestBlockNodeTemplates.ScopeCenterConnector.CanPlace(
        _manager, new BlockPos(1, 0, 0, 0), ref failureCode));
  }

  [TestMethod]
  public void CantPlaceSourceBySource() {
    _accessor.SetBlock(0, 0, 0, 0, TestBlockNodeTemplates.ScopeCenterSource);

    string failureCode = null;
    Assert.IsFalse(TestBlockNodeTemplates.ScopeCenterSource.CanPlace(
        _manager, new BlockPos(1, 0, 0, 0), ref failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }

  [TestMethod]
  public void CanPlaceSourceByEjectedBlock() {
    _accessor.SetBlock(0, 0, 0, 0, TestBlockNodeTemplates.ScopeCenterConnector);

    string failureCode = null;
    Assert.IsTrue(TestBlockNodeTemplates.ScopeCenterSource.CanPlace(
        _manager, new BlockPos(1, 0, 0, 0), ref failureCode));
  }

  [TestMethod]
  public void CantPlaceNextToTwoSources() {
    _accessor.SetBlock(0, 0, 0, 0, TestBlockNodeTemplates.ScopeCenterSource);
    _accessor.SetBlock(2, 0, 0, 0, TestBlockNodeTemplates.ScopeCenterSource);

    string failureCode = null;
    Assert.IsFalse(TestBlockNodeTemplates.ScopeCenterConnector.CanPlace(
        _manager, new BlockPos(1, 0, 0, 0), ref failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }
}