using Lambda.Network;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Tests;

[TestClass]
public class BlockNodeTemplateTest {
  private Manager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;

  void AssertEjected(BlockPos[] blocks) {
    foreach (BlockPos pos in blocks) {
      BlockNodeTemplate template = _accessor.GetBlock(pos, out Node[] nodes);
      Assert.AreEqual(_templates.FourWay, template);
      Assert.IsTrue(nodes[0].IsEjected());
      Assert.IsTrue(!nodes[0].Source.IsSet());
      Assert.IsTrue(nodes[0].HasInfDistance);
      Assert.AreEqual(Edge.Unknown, nodes[0].Parent);
    }
  }

  void AssertConnected(BlockPos[] blocks, BlockPos source) {
    foreach (BlockPos pos in blocks) {
      BlockNodeTemplate template = _accessor.GetBlock(pos, out Node[] nodes);
      Assert.AreEqual(_templates.FourWay, template);
      Assert.AreEqual(new NodePos(source, 0), nodes[0].Source,
                      $"Block {pos} does not connect to the source.");
      Assert.AreEqual(Scope.Function, nodes[0].Scope);
    }
  }

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new Manager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_accessor, _manager);
  }

  [TestMethod]
  public void ParseSource() {
    BlockNodeTemplate template = _templates.FourWaySource;
    Assert.AreEqual(1, template.Count);
    NodeTemplate nodeTemplate =
        _templates.FourWaySource.GetNodeTemplate(Edge.NorthCenter);
    Assert.IsNotNull(nodeTemplate);
    Assert.IsTrue(nodeTemplate.Source);
  }

  [TestMethod]
  public void CanPlaceNextToSource() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.FourWaySource);
    string failureCode = null;
    Assert.IsTrue(
        _templates.FourWay.CanPlace(new BlockPos(1, 0, 0, 0), out failureCode));
  }

  [TestMethod]
  public void CantPlaceSourceBySource() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.FourWaySource);

    string failureCode = null;
    Assert.IsFalse(_templates.FourWaySource.CanPlace(new BlockPos(1, 0, 0, 0),
                                                     out failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }

  [TestMethod]
  public void CanPlaceSourceByEjectedBlock() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.FourWay);

    string failureCode = null;
    Assert.IsTrue(_templates.FourWaySource.CanPlace(new BlockPos(1, 0, 0, 0),
                                                    out failureCode));
  }

  [TestMethod]
  public void CantPlaceNextToTwoSources() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.FourWaySource);
    _accessor.SetBlock(2, 0, 0, 0, _templates.FourWaySource);

    string failureCode = null;
    Assert.IsFalse(
        _templates.FourWay.CanPlace(new BlockPos(1, 0, 0, 0), out failureCode));
    Assert.AreEqual("conflictingsources", failureCode);
  }

  [TestMethod]
  public void PlaceNextToSource() {
    // Place a source block first.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    // Place a connector block next to the source.
    BlockPos connectorPos = new(1, 0, 0, 0);
    _accessor.SetBlock(connectorPos, _templates.FourWay);

    // Verify that the connector block is now connected to the source.
    Assert.IsFalse(_manager.HasPendingWork);
    BlockNodeTemplate template =
        _accessor.GetBlock(connectorPos, out Node[] nodes);
    Assert.AreEqual(_templates.FourWay, template);
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
    _accessor.SetBlock(connectorPos, _templates.FourWay);
    Assert.AreEqual(Node.InfDistance, _accessor.GetDistance(connectorPos, 0));

    // Place a source block next to the connector.
    BlockPos sourceBlock = new(1, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that the connector block is now connected to the source.
    BlockNodeTemplate template =
        _accessor.GetBlock(connectorPos, out Node[] nodes);
    Assert.AreEqual(_templates.FourWay, template);
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
      _accessor.SetBlock(pos, _templates.FourWay);
    }

    // Place a source block next to the connector.
    BlockPos sourceBlock = new(2, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);
    Assert.AreEqual(0 * _manager.DefaultDistanceIncrement,
                    _accessor.GetDistance(sourceBlock, 0));

    Assert.IsTrue(_manager.HasPendingWork);
    _manager.FinishPendingWork();

    // Verify that the connector blocks are now connected to the source.
    foreach (BlockPos pos in connectors) {
      BlockNodeTemplate template = _accessor.GetBlock(pos, out Node[] nodes);
      Assert.AreEqual(_templates.FourWay, template);
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
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    // Place connector2 with a gap between it and the source.
    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.FourWay);

    // Verify that connector2 is not connected yet.
    Assert.IsFalse(_manager.HasPendingWork);
    BlockNodeTemplate template =
        _accessor.GetBlock(connector2Pos, out Node[] nodes);
    Assert.IsFalse(nodes[0].Source.IsSet());

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.FourWay);

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
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    // Place connector2 with a gap between it and the source.
    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.NS);

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.FourWay);

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
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos connector2Pos = new(2, 0, 0, 0);
    _accessor.SetBlock(connector2Pos, _templates.FourWay);

    BlockPos connector3Pos = new(3, 0, 0, 0);
    _accessor.SetBlock(connector3Pos, _templates.NS);

    // Place connector1 between the source and connector2.
    BlockPos connector1Pos = new(1, 0, 0, 0);
    _accessor.SetBlock(connector1Pos, _templates.FourWay);

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

  [TestMethod]
  public void ThreePathsToSource() {
    // First, the following pattern is made:
    //
    //  |  |
    // -0--1-
    //  |  |
    //  |
    // -S-
    //  |
    //  |  |
    // -2--3-
    //  |  |
    //
    // Then a connector is placed next to the source. There are 3 ways for the
    // new connector to connect to the source, and any are acceptable.
    //
    //  |  |
    // -0--1-
    //  |  |
    //  |  |
    // -S--4-
    //  |  |
    //  |  |
    // -2--3-
    //  |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 1, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);
    Assert.AreEqual(0 * _manager.DefaultDistanceIncrement,
                    _accessor.GetDistance(sourceBlock, 0));

    BlockPos[] connectors = {
      new(0, 0, 2, 0), new(1, 0, 2, 0), new(0, 0, 0, 0),
      new(1, 0, 0, 0), new(1, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    // Verify that the connector blocks are now connected to the source.
    AssertConnected(connectors, sourceBlock);
  }

  [TestMethod]
  public void RemoveOnlySource() {
    _accessor.SetBlock(0, 0, 0, 0, _templates.FourWaySource);
    _accessor.RemoveBlock(0, 0, 0, 0);
  }

  [TestMethod]
  public void RemoveSourcePropagate1() {
    // First, the following pattern is made:
    //
    //  |  |
    // -S--0-
    //  |  |
    //
    // Then the source is broken, leaving the connectors disconnected.
    //
    //     |
    //    -0-
    //     |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = { new(1, 0, 0, 0) };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    _accessor.RemoveBlock(0, 0, 0, 0);
    _manager.FinishPendingWork();

    // Verify that the connector blocks are ejected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void RemoveSourcePropagate3() {
    // First, the following pattern is made:
    //
    //  |  |  |  |
    // -S--0--1--2-
    //  |  |  |  |
    //
    // Then the source is broken, leaving the connectors disconnected.
    //
    //     |  |  |
    //    -0--1--2-
    //     |  |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = { new(1, 0, 0, 0), new(2, 0, 0, 0),
                              new(3, 0, 0, 0) };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    _accessor.RemoveBlock(0, 0, 0, 0);
    _manager.FinishPendingWork();

    // Verify that the connector blocks are ejected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void RemoveConnectorPropagateTwoWays() {
    // First, the following pattern is made:
    //
    //     |
    //    -1-
    //     |
    //  |  |
    // -S--0-
    //  |  |
    //     |
    //    -2-
    //     |
    //
    // Then the connector is broken, leaving the other connectors disconnected.
    //
    //     |
    //    -1-
    //     |
    //  |
    // -S-
    //  |
    //     |
    //    -2-
    //     |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 1, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = { new(1, 0, 1, 0), new(2, 0, 2, 0),
                              new(3, 0, 0, 0) };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    BlockPos remove = new(1, 0, 1, 0);
    connectors = connectors.Remove(remove);
    _accessor.RemoveBlock(remove);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are ejected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void RemoveRedundantConnector() {
    // First, the following pattern is made:
    //
    //  |  |
    // -2--1-
    //  |  |
    //  |  |
    // -S--0-
    //  |  |
    //
    // Then the first connector is broken. The remaining connectors should
    // remain connected.
    //
    //  |  |
    // -2--1-
    //  |  |
    //  |
    // -S-
    //  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = { new(1, 0, 0, 0), new(1, 0, 1, 0),
                              new(0, 0, 1, 0) };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    BlockPos remove = new(0, 0, 1, 0);
    connectors = connectors.Remove(remove);
    _accessor.RemoveBlock(remove);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are still connected.
    AssertConnected(connectors, sourceBlock);
  }

  [TestMethod]
  public void RemoveSecondConnectorInSquare() {
    // First, the following pattern is made:
    //
    //     |  |
    //    -3--2-
    //     |  |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Then the second connector is broken. The remaining connectors should
    // remain connected.
    //
    //     |  |
    //    -3--2-
    //     |  |
    //  |  |
    // -S--0-
    //  |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      new(1, 0, 0, 0),
      new(2, 0, 0, 0),
      new(2, 0, 1, 0),
      new(1, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    BlockPos remove = new(2, 0, 0, 0);
    connectors = connectors.Remove(remove);
    _accessor.RemoveBlock(remove);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are still connected.
    AssertConnected(connectors, sourceBlock);
  }

  [TestMethod]
  public void RemoveFirstConnectorInSquare() {
    // First, the following pattern is made:
    //
    //     |  |
    //    -3--2-
    //     |  |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Then connector 0 is broken. The remaining connectors should be
    // disconnected.
    //
    //     |  |
    //    -3--2-
    //     |  |
    //  |     |
    // -S-   -1-
    //  |     |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      new(1, 0, 0, 0),
      new(2, 0, 0, 0),
      new(2, 0, 1, 0),
      new(1, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    BlockPos remove = new(1, 0, 0, 0);
    connectors = connectors.Remove(remove);
    _accessor.RemoveBlock(remove);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are ejected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void RerouteToLongerPath() {
    // First, the following pattern is made:
    //
    //  |  |  |
    // -2--3--4-
    //  |  |  |
    //  |     |
    // -S-   -5-
    //  |     |
    //  |  |  |
    // -0--1--6-
    //  |  |  |
    //
    // Then connector 0 is broken. The remaining connectors should remain
    // connected. This means that connector 1 has to connect to connector 6 (or
    // possibly 6 to 5), even though connector 6 previous had a greater distance
    // than connector 1 (or 6 to 5).
    //
    //  |  |  |
    // -2--3--4-
    //  |  |  |
    //  |     |
    // -S-   -5-
    //  |     |
    //     |  |
    //    -1--6-
    //     |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 1, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      new(0, 0, 0, 0), new(1, 0, 0, 0), new(0, 0, 2, 0), new(1, 0, 2, 0),
      new(2, 0, 2, 0), new(2, 0, 1, 0), new(2, 0, 0, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    BlockPos remove = new(0, 0, 0, 0);
    connectors = connectors.Remove(remove);
    _accessor.RemoveBlock(remove);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are still connected.
    AssertConnected(connectors, sourceBlock);
  }

  [TestMethod]
  public void BreakReplaceCycle1() {
    // First, the following pattern is made:
    //
    //     |  |
    //    -3--2-
    //     |  |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Then the source and connector 0 are broken.
    //
    //     |  |
    //    -3--2-
    //     |  |
    //        |
    //       -1-
    //        |
    //
    // Before stepping, connection 0 is placed again. This test case is tricky,
    // because the new connector 0 may try to connect to connector 3, which
    // would create a cycle. However, on the next step connector 1 will discover
    // that its parent has a higher distance, and thus connector 1 will
    // disconnect itself, and propagate that.
    //
    //     |  |
    //    -3--2-
    //     |  |
    //     |  |
    //    -0--1-
    //     |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      new(1, 0, 0, 0),
      new(2, 0, 0, 0),
      new(2, 0, 1, 0),
      new(1, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    _accessor.RemoveBlock(sourceBlock);
    _accessor.RemoveBlock(connectors[0]);
    _accessor.SetBlock(connectors[0], _templates.FourWay);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are still connected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void BreakReplaceCycle2() {
    // First, the following pattern is made:
    //
    //     |  |
    //    -1--2-
    //     |  |
    //  |  |  |
    // -S--0--3-
    //  |  |  |
    //
    // Then the source and connector 0 are broken.
    //
    //     |  |
    //    -1--2-
    //     |  |
    //        |
    //       -3-
    //        |
    //
    // Before stepping, connection 0 is placed again. This test case is tricky,
    // because the new connector 0 may try to connect to connector 3, which
    // would create a cycle. However, on the next step connector 1 will discover
    // that its parent has a higher distance, and thus connector 1 will
    // disconnect itself, and propagate that.
    //
    //     |  |
    //    -1--2-
    //     |  |
    //     |  |
    //    -0--3-
    //     |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      new(1, 0, 0, 0),
      new(1, 0, 1, 0),
      new(2, 0, 1, 0),
      new(2, 0, 0, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }

    _accessor.RemoveBlock(sourceBlock);
    _accessor.RemoveBlock(connectors[0]);
    _accessor.SetBlock(connectors[0], _templates.FourWay);
    _manager.FinishPendingWork();

    // Verify that the remaining connector blocks are still connected.
    AssertEjected(connectors);
  }

  [TestMethod]
  public void ReplaceWithShorterPath() {
    // First, the following pattern is made. Connector 7 has a shorter distance
    // than connector 5.
    //
    //  |
    // -6-
    //  |
    //  |  |  |
    // -5--4--3-
    //  |  |  |
    //  |     |
    // -7-   -2-
    //  |     |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Then connector 4 and connector 5 are broken.
    //
    //  |
    // -6-
    //  |
    //        |
    //       -3-
    //        |
    //  |     |
    // -7-   -2-
    //  |     |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Before stepping, connection 5 is placed again. Connector 5 now connects
    // to connector 7, which means the distance between connector 5 and
    // connector 6 is too great. Finishing the steps should cause connector 6's
    // distance to get fixed.
    //
    //  |
    // -6-
    //  |
    //  |     |
    // -5-   -3-
    //  |     |
    //  |     |
    // -7-   -2-
    //  |     |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      /*connectors[0]=*/new(1, 0, 0, 0),
      /*connectors[1]=*/new(2, 0, 0, 0),
      /*connectors[2]=*/new(2, 0, 1, 0),
      /*connectors[3]=*/new(2, 0, 2, 0),
      /*connectors[4]=*/new(1, 0, 2, 0),
      /*connectors[5]=*/new(0, 0, 2, 0),
      /*connectors[6]=*/new(0, 0, 3, 0),
      /*connectors[7]=*/new(0, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }
    // Verify that connector 7 has a lower distance than connector 5, meaning
    // that it connected directly to the source. This test relies on the edge
    // preference of the algorithm.
    Assert.IsTrue(_accessor.GetDistance(connectors[7], 0) <
                  _accessor.GetDistance(connectors[5], 0));

    // Verify that with the current distances, connector 6 cannot go through
    // connector 5 and connector 7.
    Assert.IsFalse(_manager.IsPropagationDistanceInRange(
        /*parent=*/_accessor.GetDistance(connectors[7], 0) +
            _manager.DefaultDistanceIncrement,
        _accessor.GetDistance(connectors[6], 0)));

    _accessor.RemoveBlock(connectors[4]);
    _accessor.RemoveBlock(connectors[5]);
    _accessor.SetBlock(connectors[5], _templates.FourWay);
    _manager.FinishPendingWork();

    // Verify that now connector 6 goes through connector 5 and connector 7 with
    // the correct distances.
    Assert.IsTrue(_manager.IsPropagationDistanceInRange(
        _accessor.GetDistance(connectors[7], 0),
        _accessor.GetDistance(connectors[5], 0)));
    Assert.IsTrue(_manager.IsPropagationDistanceInRange(
        _accessor.GetDistance(connectors[5], 0),
        _accessor.GetDistance(connectors[6], 0)));

    // Verify that all connectors (except 4 which was removed) are still
    // connected.
    AssertConnected(connectors.RemoveEntry(4), sourceBlock);
  }

  [TestMethod]
  public void RerouteShorterPath() {
    // First, the following pattern is made. Connector 7 has a shorter distance
    // than connector 5.
    //
    //  |
    // -6-
    //  |
    //  |  |  |
    // -5--4--3-
    //  |  |  |
    //  |     |
    // -7-   -2-
    //  |     |
    //  |  |  |
    // -S--0--1-
    //  |  |  |
    //
    // Then connector 0 is broken. All of the remaining connectors should
    // remain connected.
    //
    //  |
    // -6-
    //  |
    //  |  |  |
    // -5--4--3-
    //  |  |  |
    //  |     |
    // -7-   -2-
    //  |     |
    //  |     |
    // -S-   -1-
    //  |     |
    //

    // Place a source block.
    BlockPos sourceBlock = new(0, 0, 0, 0);
    _accessor.SetBlock(sourceBlock, _templates.FourWaySource);

    BlockPos[] connectors = {
      /*connectors[0]=*/new(1, 0, 0, 0),
      /*connectors[1]=*/new(2, 0, 0, 0),
      /*connectors[2]=*/new(2, 0, 1, 0),
      /*connectors[3]=*/new(2, 0, 2, 0),
      /*connectors[4]=*/new(1, 0, 2, 0),
      /*connectors[5]=*/new(0, 0, 2, 0),
      /*connectors[6]=*/new(0, 0, 3, 0),
      /*connectors[7]=*/new(0, 0, 1, 0),
    };
    foreach (BlockPos pos in connectors) {
      _accessor.SetBlock(pos, _templates.FourWay);
      _manager.FinishPendingWork();
    }
    // Verify that connector 7 has a lower distance than connector 5, meaning
    // that it connected directly to the source. This test relies on the edge
    // preference of the algorithm.
    Assert.IsTrue(_accessor.GetDistance(connectors[7], 0) <
                  _accessor.GetDistance(connectors[5], 0));

    _accessor.RemoveBlock(connectors[0]);
    _manager.FinishPendingWork();

    // Verify that all connectors (except 0 which was removed) are still
    // connected.
    AssertConnected(connectors.RemoveEntry(0), sourceBlock);
  }
}
