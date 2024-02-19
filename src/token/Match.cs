using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class Match : ConstructRoot {
  // This is the position of the match node.
  public readonly NodePos MatchPos;
  public readonly int OutputNodeId;
  public override IReadOnlyList<NodePos> Blocks {
    get {
      if (OutputNodeId == -1) {
        return new NodePos[] { MatchPos };
      } else {
        return new NodePos[] { MatchPos,
                               new NodePos(MatchPos.Block, OutputNodeId) };
      }
    }
  }

  public override ConstructRoot Construct => this;

  public readonly TermInput Input;
  private MatchIn _matchIn = null;
  private SortedSet<Case> _cases;

  public override IReadOnlyList<Token> Children {
    get {
      IEnumerable<Token> e = _cases.Cast<Token>();
      if (_matchIn != null) {
        e = e.Prepend(_matchIn);
      }
      e = e.Prepend(Input);
      return e.ToList();
    }
  }

  private readonly List<NodePos> _matchConnectors = new();
  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      _matchConnectors;

  public Match(string name, NodePos pos, int inputNodeId, int outputNodeId)
      : base(name) {
    MatchPos = pos;
    OutputNodeId = outputNodeId;
    _cases = new(new TokenComparer(BlockFacing.SOUTH));
    Input = new(
        "input",
        new NodePos(pos.Block, inputNodeId == -1 ? pos.NodeId : inputNodeId),
        this);
  }

  public override void AddConnector(TokenEmissionState state,
                                    NetworkType network, NodePos pos) {
    if (network == NetworkType.Match) {
      _matchConnectors.Add(pos);
      ReleaseRef(state, pos);
    } else {
      base.AddConnector(state, network, pos);
    }
  }

  public override void AddPendingChild(TokenEmissionState state,
                                       NetworkType network, NodePos pos) {
    if (network == NetworkType.Match) {
      AddRef(state, pos);
      state.AddPending(pos);
    } else {
      base.AddPendingChild(state, network, pos);
    }
  }

  public void AddCase(Case caseBlock) { _cases.Add(caseBlock); }

  public void AddMatchIn(MatchIn m) {
    if (_matchIn != null) {
      throw new InvalidOperationException(
          "The match already has a matchin token.");
    }
    _matchIn = m;
  }

  public override void Dispose() {
    SortedSet<Case> cases = _cases;
    _cases = null;
    if (cases != null) {
      foreach (Case c in cases) {
        c.Dispose();
      }
    }
    MatchIn matchIn = _matchIn;
    _matchIn = null;
    matchIn?.Dispose();
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

    state.WriteSubgraphNode(Input);
    state.WriteSubgraphEdge(this, Input);

    if (_matchIn != null) {
      _matchIn.WriteSubgraphNode(state);
      state.WriteSubgraphEdge(this, _matchIn);
    }

    foreach (Case c in _cases) {
      c.WriteSubgraphNode(state);
      state.WriteSubgraphEdge(this, c);
    }

    state.WriteSubgraphFooter();

    Input.WriteOutsideEdges(state);
    _matchIn?.WriteOutsideEdges(state);
    foreach (Case c in _cases) {
      c.WriteOutsideEdges(state);
    }
  }
}
