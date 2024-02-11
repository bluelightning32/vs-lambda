using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

using Lambda.CollectibleBehavior;
using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public abstract class Token {
  public abstract IReadOnlyList<NodePos> Blocks { get; }

  // The location of all scope and match connector nodes that point to this
  // block as a source.
  public abstract IReadOnlyList<NodePos> ScopeMatchConnectors { get; }

  // The location of all term connector nodes that point to this block as a
  // source.
  public abstract IReadOnlyList<NodePos> TermConnectors { get; }

  public abstract ConstructRoot Construct { get; }

  public abstract IReadOnlyList<Token> Children { get; }

  public int Depth { get; private set; }
  public int PendingRef { get; private set; }

  public string Name { get; private set; }

  private HashSet<NodePos> _pendingRefLocations = new();

  public Token(string name) {
    PendingRef = 0;
    Name = name;
  }

  public void AddRef(TokenEmission state, NodePos pos) {
    ++PendingRef;
    Debug.Assert(_pendingRefLocations.Add(pos));
    Debug.Assert(state.PreparedContains(this));
  }

  public void ReleaseRef(TokenEmission state, NodePos child) {
    Debug.Assert(_pendingRefLocations.Remove(child));
    if (--PendingRef == 0) {
      state.FinishPrepared(this);
    }
  }

  public virtual void AddConnector(TokenEmission state, NetworkType network,
                                   NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept connectors in ${network}.");
  }

  public virtual void AddPendingChild(TokenEmission state, NetworkType network,
                                      NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept children in ${network}.");
  }

  public void AddPendingChildren(TokenEmission state, NetworkType network,
                                 IEnumerable<NodePos> children) {
    foreach (NodePos child in children) {
      AddPendingChild(state, network, child);
    }
  }

  public virtual void AddSink(TokenEmission state, Token sink) {
    throw new InvalidOperationException("Token does not accept children.");
  }
}

public abstract class TermSource : Token {
  private readonly List<NodePos> _termConnectors = new();
  public override IReadOnlyList<NodePos> TermConnectors => _termConnectors;

  public TermSource(string name) : base(name) {}

  public override void AddConnector(TokenEmission state, NetworkType network,
                                    NodePos pos) {
    if (network == NetworkType.Term) {
      _termConnectors.Add(pos);
      ReleaseRef(state, pos);
    } else {
      base.AddConnector(state, network, pos);
    }
  }

  public override void AddSink(TokenEmission state, Token sink) {
    if (sink is TermInput input) {
      input.SetSource(this);
    } else {
      base.AddSink(state, sink);
    }
  }

  public override void AddPendingChild(TokenEmission state, NetworkType network,
                                       NodePos pos) {
    if (network == NetworkType.Term) {
      AddRef(state, pos);
      state.AddPending(pos);
    } else {
      base.AddPendingChild(state, network, pos);
    }
  }
}

public interface IAcceptPort {
  public Token AddPort(TokenEmission state, NodePos pos, string name,
                       bool isSource);
}

public abstract class ConstructRoot : TermSource {
  // Number of edges that point to this element.
  public int IncomingEdgeCount { get; private set; }

  public ConstructRoot(string name) : base(name) {}
}
