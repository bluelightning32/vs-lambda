using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.Network;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Token;

public class Puzzle : Function {
  public override IReadOnlyList<Token> Children {
    get {
      Token[] parameterChildren = _parameters.GetChildrenAtLevel(
          _parameters.Parameters.FirstOrDefault());
      return parameterChildren.InsertAt(_resultType, 0);
    }
  }

  private TermInput _resultType = null;

  public Puzzle(string name, NodePos pos, int outputNodeId, BlockFacing face)
      : base(name, pos, outputNodeId, face) {}

  public override Token AddPort(TokenEmitter state, NodePos pos, string name,
                                bool isSource) {
    if (name == "resulttype") {
      if (_resultType != null) {
        List<NodePos> blocks = new(_resultType.Blocks) { pos };
        throw new InvalidFormatException(blocks.ToArray(),
                                         "already-has-result");
      }
      _resultType = new TermInput("resultType", pos, this);
      return _resultType;
    }
    return base.AddPort(state, pos, name, isSource);
  }

  public override void Dispose() {
    TermInput resultType = _resultType;
    _resultType = null;
    resultType?.Dispose();
    base.Dispose();
  }

  protected override void WriteSubgraphNodes(GraphvizEmitter state) {
    if (_resultType != null) {
      state.WriteSubgraphNode(_resultType);
      state.WriteSubgraphEdge(this, _resultType);
    }
    base.WriteSubgraphNodes(state);
  }

  public override void WriteOutsideEdges(GraphvizEmitter state) {
    _resultType?.WriteOutsideEdges(state);
    base.WriteOutsideEdges(state);
  }
}
