using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.Network;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Token;

public class Function : ConstructRoot {
  public readonly NodePos ScopePos;
  public readonly int OutputNodeId;
  public override IReadOnlyList<NodePos> Blocks {
    get {
      if (OutputNodeId == -1) {
        return new NodePos[] { ScopePos };
      } else {
        return new NodePos[] { ScopePos,
                               new NodePos(ScopePos.Block, OutputNodeId) };
      }
    }
  }

  public override ConstructRoot Construct => this;

  public override IReadOnlyList<Token> Children {
    get {
      return _parameters.GetChildrenAtLevel(
          _parameters.Parameters.FirstOrDefault());
    }
  }

  private readonly List<NodePos> _scopeConnectors = new();
  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      _scopeConnectors;

  protected readonly ParameterList _parameters;

  public Function(string name, NodePos pos, int outputNodeId, BlockFacing face)
      : base(name) {
    ScopePos = pos;
    OutputNodeId = outputNodeId;
    _parameters = new ParameterList(face);
  }

  public override void AddConnector(TokenEmissionState state,
                                    NetworkType network, NodePos pos) {
    if (network == NetworkType.Scope) {
      _scopeConnectors.Add(pos);
      ReleaseRef(state, pos);
    } else {
      base.AddConnector(state, network, pos);
    }
  }

  public override void AddPendingChild(TokenEmissionState state,
                                       NetworkType network, NodePos pos) {
    if (network == NetworkType.Scope) {
      AddRef(state, pos);
      state.AddPending(pos);
    } else {
      base.AddPendingChild(state, network, pos);
    }
  }

  public virtual Token AddPort(TokenEmissionState state, NodePos pos,
                               string name, bool isSource) {
    Token added = null;
    if (isSource) {
      Parameter newParam = new("parameter", pos, this, _parameters);
      _parameters.Parameters.Add(newParam);
      added = newParam;
    } else {
      if (_parameters.Result != null) {
        List<NodePos> blocks = new(_parameters.Result.Blocks) { pos };
        throw new InvalidFormatException(blocks.ToArray(),
                                         "already-has-result");
      }
      _parameters.Result = new TermInput("result", pos, this);
      added = _parameters.Result;
    }
    return added;
  }

  public override void Dispose() {
    _parameters.Dispose();
    base.Dispose();
  }

  protected virtual void WriteSubgraphNodes(GraphvizState state) {
    Token parent = this;
    Parameter p = _parameters.Parameters.FirstOrDefault();
    while (true) {
      foreach (Token t in _parameters.GetChildrenAtLevel(p)) {
        state.WriteSubgraphNode(t);
        state.WriteSubgraphEdge(parent, t);
      }
      if (p == null) {
        break;
      }
      parent = p;
      p = _parameters.GetNext(p);
    }
  }

  public override void WriteOutsideEdges(GraphvizState state) {
    Parameter p = _parameters.Parameters.FirstOrDefault();
    while (true) {
      foreach (Token t in _parameters.GetChildrenAtLevel(p)) {
        t.WriteOutsideEdges(state);
      }
      if (p == null) {
        break;
      }
      p = _parameters.GetNext(p);
    }
  }

  public override void WriteConstruct(GraphvizState state) {
    string name = state.GetName(this);
    StringBuilder label = new();
    label.Append(Name);
    bool first = true;
    foreach (NodePos pos in Blocks) {
      if (first) {
        label.Append($"\\n{pos}");
        first = false;
      } else {
        label.Append($",\\n{pos}");
      }
    }
    state.WriteClusterHeader(name, label.ToString());

    state.WriteSubgraphNode(this);

    WriteSubgraphNodes(state);

    state.WriteClusterFooter();

    WriteOutsideEdges(state);
  }
}
