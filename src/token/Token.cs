using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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

  public abstract string Name { get; }

  // This is only used for multiuse scoping. Before the scoping is performed,
  // this is set to 0. After the scoping is done, this is set to the number of
  // parents, excluding the anchor edge.
  protected int _multiUseVisited = 0;

  private readonly HashSet<NodePos> _pendingRefLocations = new();

  public IReadOnlySet<NodePos> PendingRefLocations {
    get => _pendingRefLocations;
  }

  // Converts an arbitrary string into an identifier, by replacing invalid
  // characters with underscores.
  protected static void SanitizeIdentifier(string name, StringBuilder sb) {
    if (name.Length == 0) {
      sb.Append("empty");
      return;
    }
    if (Char.IsLetter(name[0])) {
      sb.Append(name[0]);
    } else if (Char.IsAsciiDigit(name[0])) {
      sb.Append('_');
      sb.Append(name[0]);
    } else {
      sb.Append('_');
      if (name.Length == 1) {
        // A single underscore is a reserved identifier. So double it up.
        sb.Append('_');
        return;
      }
    }

    for (int i = 1; i < name.Length; ++i) {
      char c = name[i];
      if (Char.IsLetterOrDigit(c) || c == '\'') {
        sb.Append(c);
      } else {
        sb.Append('_');
      }
    }
  }

  public virtual void GetPreferredIdentifier(StringBuilder sb) {
    SanitizeIdentifier(Name, sb);
    sb.Append('_');
    // The foreach only looks at the first position, if there are 1 or more
    // positions.
    foreach (NodePos firstPos in Blocks) {
      sb.Append(firstPos.Block.X);
      sb.Append('_');
      sb.Append(firstPos.Block.Y);
      sb.Append('_');
      sb.Append(firstPos.Block.Z);
      sb.Append('_');
      sb.Append(firstPos.Block.dimension);
      sb.Append('_');
      sb.Append(firstPos.NodeId);
      break;
    }
  }

  public Token() { PendingRef = 0; }

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

  public virtual void AddConnector(TokenEmitter state, NetworkType network,
                                   NodePos pos) {
    throw new InvalidOperationException(
        "Token does not accept connectors in ${network}.");
  }

  public virtual void AddPendingChild(TokenEmitter state, NetworkType network,
                                      NodePos pos) {
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

  public virtual void ScopeMultiuse(AnchorPoint tracker, bool isUse) {
    ScopeMultiuseVisitChildren(tracker);
    ++_multiUseVisited;
  }

  protected virtual void ScopeMultiuseVisitChildren(AnchorPoint tracker) {
    foreach (Token c in Children) {
      c.ScopeMultiuse(tracker, true);
    }
  }

  protected static void EmitReference(Token reference, CoqEmitter emitter,
                                      bool app_needs_parens) {
    if (reference == null) {
      emitter.Write('_');
      return;
    }
    reference.EmitExpression(emitter, app_needs_parens);
  }

  public abstract void EmitExpression(CoqEmitter emitter,
                                      bool app_needs_parens);
}

public interface IAcceptScopePort {
  public Token AddPort(TokenEmitter state, NodePos source, Node[] nodes,
                       string inventoryTerm, BlockPos childPos,
                       NodeTemplate child);
}
