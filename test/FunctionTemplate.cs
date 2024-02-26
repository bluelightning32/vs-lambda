using Lambda.Network;

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
      using TokenEmissionState state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 0, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
          r, i == 0 ? TestContext.FullyQualifiedTestClassName : null,
          TestContext.TestName);
      if (puzzle is Function f) {
        CollectionAssert.AreEqual(new Token[] { puzzle },
                                  state.UnreferencedRoots.ToList());

        Assert.IsTrue(
            f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
        Assert.AreEqual(new NodePos(puzzleBlock,
                                    _accessor.FindNodeId(puzzleBlock, "scope")),
                        f.ScopePos);
        Assert.AreEqual("resultType", f.Children[0].Name);
        Assert.AreEqual("nat -> nat",
                        ((Constant)f.Children[0].Children[0]).Term);
        Assert.AreEqual("parameter", f.Children[1].Name);
        Assert.IsTrue(
            f.Children[1].TermConnectors.Contains(new NodePos(3, 0, 1, 0, 0)));
        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        Assert.AreEqual(f.Children[1], f.Children[1].Children[0].Children[0]);
      } else {
        Assert.Fail();
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
      using TokenEmissionState state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 0, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
          r, i == 0 ? TestContext.FullyQualifiedTestClassName : null,
          TestContext.TestName);
      if (puzzle is Function f) {
        CollectionAssert.AreEqual(new Token[] { puzzle },
                                  state.UnreferencedRoots.ToList());

        Assert.IsTrue(
            f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
        Assert.AreEqual(new NodePos(puzzleBlock,
                                    _accessor.FindNodeId(puzzleBlock, "scope")),
                        f.ScopePos);
        Assert.AreEqual("resultType", f.Children[0].Name);
        Assert.AreEqual("nat -> nat -> nat",
                        ((Constant)f.Children[0].Children[0]).Term);

        Assert.AreEqual("parameter", f.Children[1].Name);
        Assert.AreEqual(0, f.Children[1].TermConnectors.Count);
        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        if (f.Children[1].Children[0].Children[0] is Function f2) {
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
            r, i == 0 ? TestContext.FullyQualifiedTestClassName : null,
            TestContext.TestName);
        if (puzzle is Function f) {
          CollectionAssert.AreEqual(new Token[] { puzzle },
                                    state.UnreferencedRoots.ToList());

          Assert.IsTrue(
              f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
          Assert.AreEqual(new NodePos(puzzleBlock, _accessor.FindNodeId(
                                                       puzzleBlock, "scope")),
                          f.ScopePos);
          Assert.AreEqual("resultType", f.Children[0].Name);
          Assert.AreEqual("nat -> nat -> nat",
                          ((Constant)f.Children[0].Children[0]).Term);

          Assert.AreEqual("parameter", f.Children[1].Name);
          Assert.AreEqual(3, f.Children[1].TermConnectors.Count);
          Assert.AreEqual("result", f.Children[1].Children[0].Name);
          if (f.Children[1].Children[0].Children[0] is Function f2) {
            Assert.AreEqual("result", f2.Children[0].Children[0].Name);
            Assert.AreEqual(f.Children[1],
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
  public void DanglingOutput() {
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

    for (int i = 0; i < 5; ++i) {
      using TokenEmissionState state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 0, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
          r, i == 0 ? TestContext.FullyQualifiedTestClassName : null,
          TestContext.TestName);
      if (puzzle is Function f) {
        CollectionAssert.AreEqual(new Token[] { puzzle },
                                  state.UnreferencedRoots.ToList());
        Assert.IsTrue(
            f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
        Assert.AreEqual(new NodePos(puzzleBlock,
                                    _accessor.FindNodeId(puzzleBlock, "scope")),
                        f.ScopePos);
        Assert.AreEqual("resultType", f.Children[0].Name);
        Assert.AreEqual("nat -> nat",
                        ((Constant)f.Children[0].Children[0]).Term);

        Assert.AreEqual("parameter", f.Children[1].Name);
        Assert.AreEqual(1, ((Parameter)f.Children[1]).Unused.Count);
        Assert.AreEqual(5, f.Children[1].TermConnectors.Count);
        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        Assert.AreEqual(f.Children[1], f.Children[1].Children[0].Children[0]);
      } else {
        Assert.Fail();
      }
    }
  }

  [TestMethod]
  public void RawFunctionDanglingOutput() {
    Legend legend = _templates.CreateLegend();
    // clang-format off
    const string schematic = (
"""
#i#F+
#+ #
#++o
####
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos startBlock = new(3, 0, 0, 0);
    Token start = state.Process(
        new NodePos(startBlock,
                    _accessor.FindNodeId(startBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
    if (start is Function f) {
      CollectionAssert.AreEqual(new Token[] { start },
                                state.UnreferencedRoots.ToList());
      Assert.IsTrue(
          f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 0, 0, 0)));
      Assert.AreEqual("parameter", f.Children[0].Name);
      Assert.AreEqual(3, f.Children[0].TermConnectors.Count);
      Assert.AreEqual("result", f.Children[0].Children[0].Name);
      Assert.AreEqual(f.Children[0], f.Children[0].Children[0].Children[0]);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void NestedDanglingOutput() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@########i###
#         +++o
# ##i#####+# #
# # +     +# #
# # +#i#  +o #
# # +# F+  # #
# # +++o   F+#
# #  ###   # #
# #        # #
# ########## #
#            #
##############
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 0, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
        TestContext.TestName);
    if (puzzle is Function f) {
      foreach (Token root in state.UnreferencedRoots) {
        if (root == puzzle) {
          continue;
        }
        // These are the locations of the dangling functions
        Assert.IsTrue(root.Blocks.Contains(new NodePos(11, 0, 6, 0, 0)) ||
                      root.Blocks.Contains(new NodePos(7, 0, 5, 0, 0)));
      }
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void DoubleAnd() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B, A -> B -> ((A*B) * (A*B))");
    legend.AddConstant('a', "pair");
    // clang-format off
    const string schematic = (
"""
/* A B a b */
#@#i#i#i#i#####
#      + +    #
#    a+A+A+++ #
#         + + #
#       a+A+A+o
###############
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { puzzle },
                                state.UnreferencedRoots.ToList());
    } else {
      Assert.Fail();
    }
  }
}
