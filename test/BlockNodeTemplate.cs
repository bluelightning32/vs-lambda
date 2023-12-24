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
    _templates = new TestBlockNodeTemplates(_accessor, _manager);
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

  [TestMethod]
  public void PlaceNextToSource() {
    // Place a source block first.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);

    // Place a connector block next to the source.
    BlockPos connectorPos = new(1, 0, 0, 0);
    _accessor.SetBlock(connectorPos, _templates.ScopeCenterConnector);

    // Verify that the connector block is now connected to the source.
    Assert.IsFalse(_manager.HasPendingWork);
    BlockNodeTemplate template =
        _accessor.GetBlock(connectorPos, out Node[] nodes);
    Assert.AreEqual(_templates.ScopeCenterConnector, template);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    Assert.AreEqual(Scope.Function, nodes[0].Scope);
    Assert.AreEqual(0 + _manager.DefaultDistanceIncrement,
                    nodes[0].PropagationDistance);
    Assert.AreEqual(Edge.WestCenter, nodes[0].Parent);
  }

  [TestMethod]
  public void PlacedSourcePropagates1Node() {
    // Place a connector block first.
    BlockPos connectorPos = new(0, 0, 0, 0);
    _accessor.SetBlock(connectorPos, _templates.ScopeCenterConnector);
    Assert.AreEqual(Node.InfDistance, _accessor.GetDistance(connectorPos, 0));

    // Place a source block next to the connector.
    BlockPos sourceBlock = new(1, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that the connector block is now connected to the source.
    BlockNodeTemplate template =
        _accessor.GetBlock(connectorPos, out Node[] nodes);
    Assert.AreEqual(_templates.ScopeCenterConnector, template);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    Assert.AreEqual(Scope.Function, nodes[0].Scope);
    Assert.AreEqual(0 + _manager.DefaultDistanceIncrement,
                    nodes[0].PropagationDistance);
    Assert.AreEqual(Edge.EastCenter, nodes[0].Parent);
  }

  [TestMethod]
  public void PlacedSourcePropagates2Nodes() {
    // Place a connector block first.
    BlockPos[] connectors = { new(0, 0, 0, 0), new(1, 0, 0, 0) };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.ScopeCenterConnector);
    }

    // Place a source block next to the connector.
    BlockPos sourceBlock = new(2, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);
    Assert.AreEqual(0 * _manager.DefaultDistanceIncrement,
                    _accessor.GetDistance(sourceBlock, 0));

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that the connector blocks are now connected to the source.
    foreach (BlockPos pos in connectors) {
      BlockNodeTemplate template = _accessor.GetBlock(pos, out Node[] nodes);
      Assert.AreEqual(_templates.ScopeCenterConnector, template);
      Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
      Assert.AreEqual(Scope.Function, nodes[0].Scope);
      Assert.AreEqual(Edge.EastCenter, nodes[0].Parent);
    }
    Assert.AreEqual(1 * _manager.DefaultDistanceIncrement,
                    _accessor.GetDistance(connectors[1], 0));
    Assert.AreEqual(2 * _manager.DefaultDistanceIncrement,
                    _accessor.GetDistance(connectors[0], 0));
  }

  [TestMethod]
  public void PlacedConnectorPropagates() {
    // Place a source block first.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);

    // Place connector2 with a gap between it and the source.
    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.ScopeCenterConnector);

    // Verify that connector2 is not connected yet.
    Assert.IsFalse(_manager.HasPendingWork);
    BlockNodeTemplate template =
        _accessor.GetBlock(connector2Pos, out Node[] nodes);
    Assert.IsFalse(nodes[0].Source.IsSet());

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.ScopeCenterConnector);

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that connector1 and connector2 are connected to the source.
    template = _accessor.GetBlock(connector1Pos, out nodes);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    template = _accessor.GetBlock(connector2Pos, out nodes);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
  }

  [TestMethod]
  public void PlacedConnectorUnrequiredEdge() {
    // First, the following pattern is made:
    //
    //  |     |
    // -S-    2
    //  |     |
    //
    // Then a connector is placed in the middle. The connector can propagate
    // from the source, but not to the other connector, because it does not have
    // a westward edge.
    //
    //  |     |
    // -S--1- 2
    //  |     |
    //
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);

    // Place connector2 with a gap between it and the source.
    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.ScopeNSCenterConnector);

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.ScopeCenterConnector);

    _manager.FinishPendingWork();

    // Verify that connector1 is connected to the source but not connector2.
    BlockNodeTemplate template =
        _accessor.GetBlock(connector1Pos, out Node[] nodes);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    template = _accessor.GetBlock(connector2Pos, out nodes);
    Assert.IsFalse(nodes[0].Source.IsSet());
  }

  [TestMethod]
  public void ProgatedConnectorUnrequiredEdge() {
    // First, the following pattern is made:
    //
    //  |     |  |
    // -S-   -2- 3
    //  |     |  |
    //
    // Then a connector is placed next to the source. The placed connector can
    // propagate over one block, but not a second, because its edges do not
    // connect.
    //
    //  |  |  |  |
    // -S--1--2- 3
    //  |  |  |  |
    //
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.ScopeCenterSource);

    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.ScopeCenterConnector);

    BlockPos connector3Pos = new(3, 0, 0, 0);
    _accessor.SetBlock(connector3Pos, _templates.ScopeNSCenterConnector);

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.ScopeCenterConnector);

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that connector1 and connector2 are connected to the source but not
    // connector3.
    BlockNodeTemplate template =
        _accessor.GetBlock(connector1Pos, out Node[] nodes);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    template = _accessor.GetBlock(connector2Pos, out nodes);
    Assert.AreEqual(new NodePos(sourceBlock, 0), nodes[0].Source);
    template = _accessor.GetBlock(connector3Pos, out nodes);
    Assert.IsFalse(nodes[0].Source.IsSet());
  }
}