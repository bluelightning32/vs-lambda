using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class Case : Token {
  public readonly NodePos MatchPos;
  public readonly int ScopeNodeId;
  public override IReadOnlyList<NodePos> Blocks {
    get {
      return new NodePos[] { MatchPos,
                             new NodePos(MatchPos.Block, ScopeNodeId) };
    }
  }

  private Match _match;

  public override ConstructRoot Construct => _match;

  public override IReadOnlyList<Token> Children =>
      _parameters.GetChildrenAtLevel(_parameters.Parameters.FirstOrDefault());

  private readonly List<NodePos> _scopeConnectors = new();
  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      _scopeConnectors;

  public override IReadOnlyList<NodePos> TermConnectors =>
      Array.Empty<NodePos>();

  private readonly ParameterList _parameters;

  public Case(string name, NodePos matchPos, Match match, int scopeNodeId,
              BlockFacing face)
      : base(name) {
    MatchPos = matchPos;
    ScopeNodeId = scopeNodeId;
    if (MatchPos.NodeId == scopeNodeId) {
      throw new ArgumentException(
          $"The match node id {MatchPos.NodeId} matches the scope node id {scopeNodeId}.");
    }
    _parameters = new ParameterList(face);
    _match = match;
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
    Token added;
    if (isSource) {
      Parameter newParam = new("parameter", pos, _match, _parameters);
      _parameters.Parameters.Add(newParam);
      added = newParam;
    } else {
      if (_parameters.Result != null) {
        List<NodePos> blocks = new(_parameters.Result.Blocks) { pos };
        throw new InvalidFormatException(blocks.ToArray(),
                                         "already-has-result");
      }
      _parameters.Result = new TermInput("result", pos, _match);
      added = _parameters.Result;
    }
    return added;
  }

  public override void Dispose() {
    _match = null;
    _parameters.Dispose();
    _scopeConnectors.Clear();
    base.Dispose();
  }

  public void WriteSubgraphNode(GraphvizState state) {
    string name = state.GetName(this);
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
}
