using Lambda.Network;
using Lambda.Token;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class FunctionTemplateTest {
  private Manager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;

  public TestContext TestContext { get; set; }

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new Manager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_manager);
  }

  private void MaybeWriteGraphviz(TokenEmissionState state) {
    // To save graphviz files, run the tests with:
    // dotnet test -c Debug --logger:"console;verbosity=detailed" -e GRAPHVIZ=1
    if (Environment.GetEnvironmentVariable("GRAPHVIZ") == null) {
      return;
    }
    using StreamWriter writer = new(TestContext.TestName + ".gv.txt");
    state.SaveGraphviz(TestContext.TestName, writer);
  }

  [TestMethod]
  public void EmitPassthrough() {
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

    for (int i = 0; i < 5; ++i) {
      using (TokenEmissionState state = new(_accessor)) {
        Random r = new(i);
        BlockPos puzzleBlock = new(1, 0, 0, 0);
        Token puzzle = state.Process(
            new NodePos(puzzleBlock,
                        _accessor.FindNodeId(puzzleBlock, "scope")),
            new Random(i));
        if (i == 0) {
          MaybeWriteGraphviz(state);
        }
        if (puzzle is Function f) {
          CollectionAssert.AreEqual(new Token[] { puzzle },
                                    state.UnreferencedRoots.ToList());

          Assert.IsTrue(
              f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
          Assert.AreEqual(new NodePos(puzzleBlock, _accessor.FindNodeId(
                                                       puzzleBlock, "scope")),
                          f.ScopePos);
          Assert.AreEqual("parameter", f.Children[0].Name);
          Assert.IsTrue(f.Children[0].TermConnectors.Contains(
              new NodePos(3, 0, 1, 0, 0)));
          Assert.AreEqual("resultType", f.Children[0].Children[0].Name);
          Assert.AreEqual(
              "nat -> nat",
              ((Constant)f.Children[0].Children[0].Children[0]).Term);
          Assert.AreEqual("result", f.Children[0].Children[1].Name);
          Assert.AreEqual(f.Children[0], f.Children[0].Children[1].Children[0]);
        } else {
          Assert.Fail();
        }
      }
    }
  }

  [TestMethod]
  public void NestedPassthrough() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@#i####
#      #
# #i#F+o
# #+ # #
# #++o #
# #### #
#      #
########
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    for (int i = 0; i < 5; ++i) {
      using (TokenEmissionState state = new(_accessor)) {
        Random r = new(i);
        BlockPos puzzleBlock = new(1, 0, 0, 0);
        Token puzzle = state.Process(
            new NodePos(puzzleBlock,
                        _accessor.FindNodeId(puzzleBlock, "scope")),
            new Random(i));
        if (i == 0) {
          MaybeWriteGraphviz(state);
        }
        if (puzzle is Function f) {
          CollectionAssert.AreEqual(new Token[] { puzzle },
                                    state.UnreferencedRoots.ToList());

          Assert.IsTrue(
              f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
          Assert.AreEqual(new NodePos(puzzleBlock, _accessor.FindNodeId(
                                                       puzzleBlock, "scope")),
                          f.ScopePos);
          Assert.AreEqual("parameter", f.Children[0].Name);
          Assert.AreEqual(0, f.Children[0].TermConnectors.Count);
          Assert.AreEqual("resultType", f.Children[0].Children[0].Name);
          Assert.AreEqual(
              "nat -> nat -> nat",
              ((Constant)f.Children[0].Children[0].Children[0]).Term);
          Assert.AreEqual("result", f.Children[0].Children[1].Name);
          if (f.Children[0].Children[1].Children[0] is Function f2) {
            Assert.AreEqual("result", f2.Children[0].Children[0].Name);
            Assert.AreEqual(f2.Children[0],
                            f2.Children[0].Children[0].Children[0]);
          } else {
            Assert.Fail();
          }
        } else {
          Assert.Fail();
        }
      }
    }
  }

  [TestMethod]
  public void NestedScopeFirst() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@###i###
#    +  #
# #i#+# #
# #  +o #
# #   F+o
# ##### #
#       #
#########
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    for (int i = 0; i < 5; ++i) {
      using (TokenEmissionState state = new(_accessor)) {
        Random r = new(i);
        BlockPos puzzleBlock = new(1, 0, 0, 0);
        Token puzzle = state.Process(
            new NodePos(puzzleBlock,
                        _accessor.FindNodeId(puzzleBlock, "scope")),
            new Random(i));
        if (i == 0) {
          MaybeWriteGraphviz(state);
        }
        if (puzzle is Function f) {
          CollectionAssert.AreEqual(new Token[] { puzzle },
                                    state.UnreferencedRoots.ToList());

          Assert.IsTrue(
              f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
          Assert.AreEqual(new NodePos(puzzleBlock, _accessor.FindNodeId(
                                                       puzzleBlock, "scope")),
                          f.ScopePos);
          Assert.AreEqual("parameter", f.Children[0].Name);
          Assert.AreEqual(3, f.Children[0].TermConnectors.Count);
          Assert.AreEqual("resultType", f.Children[0].Children[0].Name);
          Assert.AreEqual(
              "nat -> nat -> nat",
              ((Constant)f.Children[0].Children[0].Children[0]).Term);
          Assert.AreEqual("result", f.Children[0].Children[1].Name);
          if (f.Children[0].Children[1].Children[0] is Function f2) {
            Assert.AreEqual("result", f2.Children[0].Children[0].Name);
            Assert.AreEqual(f.Children[0],
                            f2.Children[0].Children[0].Children[0]);
          } else {
            Assert.Fail();
          }
        } else {
          Assert.Fail();
        }
      }
    }
  }
}