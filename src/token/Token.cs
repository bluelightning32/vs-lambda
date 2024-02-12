using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Lambda.Network;

namespace Lambda.Token;

public abstract class Token : IDisposable {
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

  private readonly HashSet<NodePos> _pendingRefLocations = new();

  public Token(string name) {
    PendingRef = 0;
    Name = name;
  }

  public void AddRef(TokenEmissionState state, NodePos pos) {
    ++PendingRef;
    Debug.Assert(_pendingRefLocations.Add(pos), $"Position {pos} already referenced the block.");
    Debug.Assert(state.PreparedContains(this));
  }

  public void ReleaseRef(TokenEmissionState state, NodePos child) {
    Debug.Assert(_pendingRefLocations.Remove(child));
    if (--PendingRef == 0) {
      state.FinishPrepared(this);
    }
  }

  public virtual void AddConnector(TokenEmissionState state,
                                   NetworkType network, NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept connectors in ${network}.");
  }

  public virtual void AddPendingChild(TokenEmissionState state,
                                      NetworkType network, NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept children in ${network}.");
  }

  public void AddPendingChildren(TokenEmissionState state, NetworkType network,
                                 IEnumerable<NodePos> children) {
    foreach (NodePos child in children) {
      AddPendingChild(state, network, child);
    }
  }

  public virtual void AddSink(TokenEmissionState state, Token sink) {
    throw new InvalidOperationException("Token does not accept children.");
  }

  // Breaks reference cycles that this Token is part of.
  //
  // Disposing the token is not strictly necessary. It only breaks reference
  // cycles, which makes it easier for the garbage collector to see that the
  // tokens are no longer externally referenced.
  public virtual void Dispose() { GC.SuppressFinalize(this); }

  // Write all of the edges that point outside of the construct. This does not
  // recurse with the construct or outside the construct.
  public virtual void WriteOutsideEdges(GraphvizState state) {
    foreach (Token t in Children) {
      if (t.Construct != Construct) {
        state.WriteEdge(this, t);
      }
    }
  }
}

public abstract class TermSource : Token {
  private readonly List<NodePos> _termConnectors = new();
  public override IReadOnlyList<NodePos> TermConnectors => _termConnectors;

  public TermSource(string name) : base(name) { }

  public override void AddConnector(TokenEmissionState state,
                                    NetworkType network, NodePos pos) {
    if (network == NetworkType.Term) {
      _termConnectors.Add(pos);
      ReleaseRef(state, pos);
    } else {
      base.AddConnector(state, network, pos);
    }
  }

  public override void AddSink(TokenEmissionState state, Token sink) {
    if (sink is TermInput input) {
      input.SetSource(this);
    } else {
      base.AddSink(state, sink);
    }
  }

  public override void AddPendingChild(TokenEmissionState state,
                                       NetworkType network, NodePos pos) {
    if (network == NetworkType.Term) {
      AddRef(state, pos);
      state.AddPending(pos);
    } else {
      base.AddPendingChild(state, network, pos);
    }
  }
}

public interface IAcceptPort {
  public Token AddPort(TokenEmissionState state, NodePos pos, string name,
                       bool isSource);
}

public abstract class ConstructRoot : TermSource {
  // Number of edges that point to this element.
  public int IncomingEdgeCount { get; private set; }

  public ConstructRoot(string name) : base(name) { }

  public abstract void WriteConstruct(GraphvizState state);

  public override void AddSink(TokenEmissionState state, Token sink) {
    if (sink is TermInput input) {
      input.SetSource(this);
      ++IncomingEdgeCount;
    } else {
      base.AddSink(state, sink);
    }
  }
}