using System;
using System.Collections.Generic;
using System.Linq;

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

  public override void AddConnector(TokenEmission state, NetworkType network,
                                    NodePos pos) {
    if (network == NetworkType.Scope) {
      _scopeConnectors.Add(pos);
      ReleaseRef(state, pos);
    } else {
      base.AddConnector(state, network, pos);
    }
  }

  public override void AddPendingChild(TokenEmission state, NetworkType network,
                                       NodePos pos) {
    if (network == NetworkType.Scope) {
      AddRef(state, pos);
      state.AddPending(pos);
    } else {
      base.AddPendingChild(state, network, pos);
    }
  }

  public Token AddPort(TokenEmission state, NodePos pos, string name,
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
}