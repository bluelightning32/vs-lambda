using System;
using System.Collections.Generic;
using System.Diagnostics;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public abstract class Token : IDisposable {
  public abstract IReadOnlyList<NodePos> Blocks { get; }

  public virtual NodePos FirstBlock { get => Blocks[0]; }

  // The location of all scope and match connector nodes that point to this
  // block as a source.
  public abstract IReadOnlyList<NodePos> ScopeMatchConnectors { get; }

  // The location of all term connector nodes that point to this block as a
  // source.
  public abstract IReadOnlyList<NodePos> TermConnectors { get; }

  public abstract ConstructRoot Construct { get; }

  public abstract IReadOnlyList<Token> Children { get; }

  public int PendingRef { get; private set; }

  public string Name { get; private set; }

  private readonly HashSet<NodePos> _pendingRefLocations = new();

  public IReadOnlySet<NodePos> PendingRefLocations {
    get => _pendingRefLocations;
  }

  public Token(string name) {
    PendingRef = 0;
    Name = name;
  }

  public void AddRef(TokenEmitter state, NodePos pos) {
    if (Blocks[0] == new NodePos(0, 0, 2, 0, 2) &&
        pos == new NodePos(2, 0, 2, 0, 0)) {
      PendingRef = PendingRef;
    }
    ++PendingRef;
    if (!_pendingRefLocations.Add(pos)) {
      Debug.Assert(false, $"Position {pos} already referenced the block.");
    }
    Debug.Assert(state.PreparedContains(this));
  }

  public void ReleaseRef(TokenEmitter state, NodePos child) {
    if (!_pendingRefLocations.Remove(child)) {
      Debug.Assert(false);
    }
    if (--PendingRef == 0) {
      state.FinishPrepared(this);
    }
  }

  public virtual void AddConnector(TokenEmitter state,
                                   NetworkType network, NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept connectors in ${network}.");
  }

  public virtual void AddPendingChild(TokenEmitter state,
                                      NetworkType network, NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept children in ${network}.");
  }

  public void AddPendingChildren(TokenEmitter state, NetworkType network,
                                 IEnumerable<NodePos> children) {
    foreach (NodePos child in children) {
      AddPendingChild(state, network, child);
    }
  }

  public virtual void AddSink(TokenEmitter state, Token sink) {
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
  public virtual void WriteOutsideEdges(GraphvizEmitter state) {
    foreach (Token t in Children) {
      if (t.Construct != Construct) {
        state.WriteEdge(this, t);
      }
    }
  }
}

public interface IAcceptScopePort {
  public Token AddPort(TokenEmitter state, NodePos source, Node[] nodes,
                       string inventoryTerm, BlockPos childPos,
                       NodeTemplate child);
}