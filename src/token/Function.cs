using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class Function : ConstructRoot, IAcceptPort {
  public readonly NodePos Pos;
  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { Pos };

  public override ConstructRoot Construct => this;

  public override IReadOnlyList<Token> Children =>
      _parameters.GetChildrenAtLevel(_parameters.Parameters.FirstOrDefault());

  private readonly List<NodePos> _scopeConnectors = new();
  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      _scopeConnectors;

  private readonly ParameterList _parameters;

  public Function(string name, NodePos pos, BlockFacing face) : base(name) {
    Pos = pos;
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

  public Token AddPort(TokenEmissionState state, NodePos pos, string name,
                       bool isSource) {
    Token added = null;
    if (name == "resulttype") {
      if (_parameters.ResultType != null) {
        List<NodePos> blocks = new(_parameters.ResultType.Blocks) { pos };
        throw new InvalidFormatException(blocks.ToArray(),
                                         "already-has-result");
      }
      _parameters.ResultType = new TermInput("resultType", pos, this);
      added = _parameters.ResultType;
    } else if (isSource) {
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
    state.WriteSubgraphHeader(name, label.ToString());

    state.WriteSubgraphNode(name, Name);

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

    state.WriteSubgraphFooter();

    p = _parameters.Parameters.FirstOrDefault();
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
}