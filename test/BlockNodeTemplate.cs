using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory.Tests;

[TestClass]
public class BlockNodeTemplateTest {
  private NetworkManager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new NetworkManager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_manager);
  }

  [TestMethod]
  public void ParseSource() {
    BlockNodeTemplate template = _templates.ScopeCenterSource;
    Assert.AreEqual(1, template.Count);
    NodeTemplate nodeTemplate =
        _templates.ScopeCenterSource.GetNodeTemplate(true, Edge.NorthCenter);
    Assert.IsNotNull(nodeTemplate);
    Assert.IsTrue(nodeTemplate.Source);
  }

  [TestMethod]
  public void CanPlaceNextToSource() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.ScopeCenterSource);
    string failureCode = null;
    Assert.IsTrue(_templates.ScopeCenterConnector.CanPlace(
        new BlockPos(1, 0, 0, 0), ref failureCode));
  }

  [TestMethod]
  public void CantPlaceSourceBySource() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.ScopeCenterSource);

    string failureCode = null;
    Assert.IsFalse(_templates.ScopeCenterSource.CanPlace(
        new BlockPos(1, 0, 0, 0), ref failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }

  [TestMethod]
  public void CanPlaceSourceByEjectedBlock() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.ScopeCenterConnector);

    string failureCode = null;
    Assert.IsTrue(_templates.ScopeCenterSource.CanPlace(
        new BlockPos(1, 0, 0, 0), ref failureCode));
  }

  [TestMethod]
  public void CantPlaceNextToTwoSources() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.ScopeCenterSource);
    _accessor.SetBlock(2, 0, 0, 0, _templates.ScopeCenterSource);

    string failureCode = null;
    Assert.IsFalse(_templates.ScopeCenterConnector.CanPlace(
        new BlockPos(1, 0, 0, 0), ref failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }
}