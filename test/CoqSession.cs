using Lambda.Network;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class CoqSessionTest {
  private Manager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;
  private ServerConfig _config;
  private CoqSession _session;

  public TestContext TestContext { get; set; }

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new Manager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_manager);
    _config = new() { CoqTmpDir = "." };
    _config.ResolveCoqcPath();
    _session = new CoqSession(_config);
  }

  [TestMethod]
  public void PassthroughCorrect() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@#i##
#  + #
#  ++o
######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    BlockPos puzzleBlock = new(1, 0, 0, 0);
    state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")));
    state.PostProcess();

    Assert.IsTrue(_session.ValidateCoq(state).Successful);
  }

  [TestMethod]
  public void UnresolvedImplicitArgument() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@###i###
#    +++o
# #i#+# #
# #  +o #
# #   F+#
# ##### #
#       #
#########
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    BlockPos puzzleBlock = new(1, 0, 0, 0);
    state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")));
    state.PostProcess();

    CoqResult result = _session.ValidateCoq(state);
    Assert.IsFalse(result.Successful);
    Assert.IsTrue(result.ErrorMessage.Contains("Cannot infer the type of a parameter."));
    Assert.AreEqual(1, result.ErrorLocations.Count);
    Assert.AreEqual(new BlockPos(3, 0, 2, 0), result.ErrorLocations.First().Block);
    Assert.IsFalse(result.ErrorMessage.Contains("line"));
  }

  [TestMethod]
  public void SInfo() {
    TermInfo info = _session.GetTermInfo(new BlockPos(0, 0, 0, 0),
                                         Array.Empty<string>(), "S");
    Assert.AreEqual(null, info.ErrorMessage);
    Assert.AreEqual("S", info.Term);
    Assert.AreEqual("nat -> nat", info.Type);
    Assert.AreEqual("nat", info.Constructs);
    Assert.IsFalse(info.IsType);
    Assert.IsFalse(info.IsTypeFamily);
  }

  [TestMethod]
  public void Pair1() {
    TermInfo info = _session.GetTermInfo(new BlockPos(0, 0, 0, 0),
                                         Array.Empty<string>(), "pair 1");
    Assert.AreEqual(null, info.ErrorMessage);
    Assert.AreEqual("pair 1", info.Term);
    Assert.AreEqual(null, info.Constructs);
    Assert.AreEqual("?B -> nat * ?B", info.Type);
    Assert.IsFalse(info.IsType);
    Assert.IsFalse(info.IsTypeFamily);
  }

  [TestMethod]
  public void ZInfo() {
    TermInfo info = _session.GetTermInfo(
        new BlockPos(0, 0, 0, 0), new string[] { "Coq.ZArith.ZArith" }, "Z");
    Assert.AreEqual(null, info.ErrorMessage);
    Assert.AreEqual("Z", info.Term);
    Assert.AreEqual(null, info.Constructs);
    Assert.AreEqual("Set", info.Type);
    Assert.IsTrue(info.IsType);
    Assert.IsFalse(info.IsTypeFamily);
  }

  [TestMethod]
  public void PairPairS() {
    TermInfo info = _session.GetTermInfo(new BlockPos(0, 0, 0, 0),
                                         Array.Empty<string>(), "(pair, S)");
    Assert.AreEqual(null, info.ErrorMessage);
    Assert.AreEqual("(pair, S)", info.Term);
    Assert.AreEqual(null, info.Constructs);
    Assert.AreEqual("(?A -> ?B -> ?A * ?B) * (nat -> nat)", info.Type);
    Assert.IsFalse(info.IsType);
    Assert.IsFalse(info.IsTypeFamily);
  }

  [TestMethod]
  public void Destruct5() {
    DestructInfo info = _session.GetDestructInfo(new BlockPos(0, 0, 0, 0),
                                                 Array.Empty<string>(), "5");
    Assert.AreEqual(null, info.ErrorMessage);
    Assert.AreEqual("S", info.Terms[0].Term);
    Assert.AreEqual("nat -> nat", info.Terms[0].Type);
    Assert.AreEqual("nat", info.Terms[0].Constructs);
    Assert.AreEqual("4", info.Terms[1].Term);
    Assert.AreEqual("nat", info.Terms[1].Type);
  }

  [TestMethod]
  public void DestructProdHasError() {
    DestructInfo info = _session.GetDestructInfo(
        new BlockPos(0, 0, 0, 0), Array.Empty<string>(), "nat -> nat");
    Assert.AreEqual("The term does not reduce to a constructed inductive type.",
                    info.ErrorMessage);
    Assert.AreEqual(null, info.Terms);
  }

  [TestMethod]
  public void PartiallyAppliedHasError() {
    DestructInfo info = _session.GetDestructInfo(new BlockPos(0, 0, 0, 0),
                                                 Array.Empty<string>(), "S");
    Assert.AreEqual("The term is not fully applied.", info.ErrorMessage);
    Assert.AreEqual(null, info.Terms);
  }
}
