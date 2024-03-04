using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Token;

public class Puzzle : Function {
  public override IReadOnlyList<Token> Children {
    get {
      Token[] parameterChildren = _parameters.GetChildrenAtLevel(
          _parameters.Parameters.FirstOrDefault());
      return parameterChildren.InsertAt(_resultTypeChild, 0);
    }
  }

  private TermInput _resultTypeChild = null;
  private string _resultType = null;

  public override string Name => "puzzle";

  public Puzzle(string name, NodePos pos, int outputNodeId, BlockFacing face)
      : base(name, pos, outputNodeId, face) {}

  public Token AddResultType(TokenEmitter state, string resultType) {
    if (_resultTypeChild != null) {
      List<NodePos> blocks = new(_resultTypeChild.Blocks) { FirstBlock };
      throw new InvalidFormatException(blocks.ToArray(), "already-has-result");
    }
    _resultType = resultType;
    _resultTypeChild = new TermInput("resultType", FirstBlock, this);
    Token constant = new Constant(FirstBlock, _resultType);
    constant.AddSink(state, _resultTypeChild);
    return _resultTypeChild;
  }

  public override void Dispose() {
    TermInput resultType = _resultTypeChild;
    _resultTypeChild = null;
    resultType?.Dispose();
    base.Dispose();
  }

  protected override void WriteSubgraphNodes(GraphvizEmitter state) {
    if (_resultTypeChild != null) {
      state.WriteSubgraphNode(_resultTypeChild);
      state.WriteSubgraphEdge(this, _resultTypeChild);
    }
    base.WriteSubgraphNodes(state);
  }

  public override void WriteOutsideEdges(GraphvizEmitter state) {
    _resultTypeChild?.WriteOutsideEdges(state);
    base.WriteOutsideEdges(state);
  }

  protected override void EmitDefinitionType(CoqEmitter emitter) {
    if (_resultTypeChild != null) {
      emitter.Write(':', this);
      emitter.Write(' ', null);
      EmitReference(_resultTypeChild, emitter, false);
    }
  }
}
