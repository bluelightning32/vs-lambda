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
      using TokenEmitter state = new(_accessor);
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

        Assert.AreEqual(
"""
Definition d: (nat -> nat):=
fun parameter_3_0_0_0_2 =>
  parameter_3_0_0_0_2.

""", state.EmitDefinition("d"));
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
      using TokenEmitter state = new(_accessor);
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

        Assert.AreEqual(
"""
Definition d: (nat -> nat -> nat):=
fun parameter_3_0_0_0_2 =>
  fun parameter_3_0_2_0_2 =>
    parameter_3_0_2_0_2.

""", state.EmitDefinition("d"));
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
      using TokenEmitter state = new(_accessor);

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
        Assert.AreEqual(3, f.Children[1].TermConnectors.Count);
        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        if (f.Children[1].Children[0].Children[0] is Function f2) {
          Assert.AreEqual("result", f2.Children[0].Children[0].Name);
          Assert.AreEqual(f.Children[1],
                          f2.Children[0].Children[0].Children[0]);
        } else {
          Assert.Fail();
        }

        Assert.AreEqual(
"""
Definition d: (nat -> nat -> nat):=
fun parameter_5_0_0_0_2 =>
  fun parameter_3_0_2_0_2 =>
    parameter_5_0_0_0_2.

""", state.EmitDefinition("d"));
      } else {
        Assert.Fail();
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
      using TokenEmitter state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 0, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
          r, i == 0 ? TestContext.FullyQualifiedTestClassName : null,
          TestContext.TestName);
      state.SetPuzzleParameters(new string[] { "n" });
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

        Assert.AreEqual("n", f.Children[1].Name);
        Assert.AreEqual(1,
                        ((TermInput)f.Children[1].Children[0]).Anchored.Count);
        Assert.AreEqual(5, f.Children[1].TermConnectors.Count);
        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        Assert.AreEqual(f.Children[1], f.Children[1].Children[0].Children[0]);

        string coq = state.EmitDefinition("d", out CoqEmitter emitter);

        Assert.AreEqual(
"""
Definition d: (nat -> nat):=
fun n =>
  let function_6_0_4_0_0 :=
    fun parameter_3_0_2_0_2 =>
      n in
  n.

""", coq);

        Assert.IsTrue(new HashSet<Token>() { f.Children[0].Children[0] }
                      .SetEquals(emitter.FindOverlapping(0, 14, 0, 26)));
        Assert.IsTrue(new HashSet<Token>() { f }
                      .SetEquals(emitter.FindOverlapping(1, 0, 1, 3)));
        Assert.IsTrue(new HashSet<Token>() { f, f.Children[0].Children[0] }
                      .SetEquals(emitter.FindOverlapping(0, 0, 0, 28)));
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

    using TokenEmitter state = new(_accessor);
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

    using TokenEmitter state = new(_accessor);
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

      Assert.AreEqual(
"""
Definition d: (nat -> nat):=
fun parameter_10_0_0_0_2 =>
  let function_11_0_6_0_0 :=
    fun parameter_4_0_2_0_2 =>
      let function_7_0_5_0_0 :=
        fun parameter_6_0_4_0_2 =>
          parameter_4_0_2_0_2 in
      parameter_10_0_0_0_2 in
  parameter_10_0_0_0_2.

""", state.EmitDefinition("d"));
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

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { puzzle },
                                state.UnreferencedRoots.ToList());
      TermInput result = (TermInput)f.Children[1]
                             .Children[0]
                             .Children[0]
                             .Children[0]
                             .Children[0];
      Assert.AreEqual(1, result.Anchored.Count);

      Assert.AreEqual(
"""
Definition d: (forall A B, A -> B -> ((A*B) * (A*B))):=
fun parameter_3_0_1_0_2 parameter_5_0_1_0_2 parameter_7_0_1_0_2 parameter_9_0_1_0_2 =>
  let app_9_0_3_0_2 :=
    pair parameter_7_0_1_0_2 parameter_9_0_1_0_2 in
  pair app_9_0_3_0_2 app_9_0_3_0_2.

""", state.EmitDefinition("d"));
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void NestedMultiuse() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A, a -> (A * A) * (A * A)");
    legend.AddConstant('a', "pair");
    // clang-format off
    const string schematic = (
"""
/* A a */
#@#i#i#######
#    +      #
#    +++    #
#    + +    #
#  a+A+A+++ #
#  +    + + #
#  +++++A+A+o
#############
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { puzzle },
                                state.UnreferencedRoots.ToList());
      TermInput t1 = (TermInput)f.Children[1].Children[0].Children[0];
      Assert.AreEqual(2, t1.Anchored.Count);
      Assert.AreEqual("pair", t1.Anchored[0].Name);

      Assert.AreEqual(
"""
Definition d: (forall A, a -> (A * A) * (A * A)):=
fun parameter_3_0_1_0_2 parameter_5_0_1_0_2 =>
  let pair_3_0_5_0_0 :=
    pair in
  let app_7_0_5_0_2 :=
    pair_3_0_5_0_0 parameter_5_0_1_0_2 parameter_5_0_1_0_2 in
  pair_3_0_5_0_0 app_7_0_5_0_2 app_7_0_5_0_2.

""", state.EmitDefinition("d"));
    } else {
      Assert.Fail();
    }
  }
}
