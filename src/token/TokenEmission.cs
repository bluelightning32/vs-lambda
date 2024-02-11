using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

using Lambda.Network;

namespace Lambda.Token;

public class TokenEmission {
  private readonly NodeAccessor _accessor;
  // Nodes that have already been created but whose references have not been
  // fully visited yet.
  private readonly Dictionary<NodePos, Token> _prepared = new();
  public IReadOnlyDictionary<NodePos, Token> Prepared { get => _prepared; }
  private readonly List<NodePos> _pending = new();
  private readonly List<ConstructRoot> _unreferencedRoots = new();

  public TokenEmission(NodeAccessor accessor) { _accessor = accessor; }

  private Token EmitPos(NodePos pos) {
    BlockNodeTemplate template = _accessor.GetBlock(pos.Block, out Node[] nodes,
                                                    out string inventoryTerm);
    return template.Emit(this, pos, nodes, inventoryTerm);
  }

  public Token Process(NodePos start) {
    Token result = EmitPos(start);
    while (_pending.Count != 0) {
      NodePos popped = _pending[_pending.Count - 1];
      _pending.RemoveAt(_pending.Count - 1);
      EmitPos(popped);
    }
    Debug.Assert(_pending.Count == 0);
    Debug.Assert(
        _prepared.Count == 0,
        $"Prepared nodes were not cleared, remaining={_prepared.Count} first={_prepared.First()}");
    return result;
  }

  // Emit all of the nodes starting at `start`, while processing the pending
  // queue in a randomized order.
  public Token Process(NodePos start, Random random) {
    Token result = EmitPos(start);
    while (_pending.Count != 0) {
      ShufflePending(random);
      NodePos popped = _pending[_pending.Count - 1];
      _pending.RemoveAt(_pending.Count - 1);
      EmitPos(popped);
    }
    Debug.Assert(_pending.Count == 0);
    Debug.Assert(_prepared.Count == 0);
    return result;
  }

  public void ShufflePending(Random random) {
    for (int i = 0; i < _pending.Count; ++i) {
      int swapWith = i + random.Next(_pending.Count - i);
      (_pending[swapWith], _pending[i]) = (_pending[i], _pending[swapWith]);
    }
  }

  public void FinishPrepared(Token token) {
    bool removed = false;
    foreach (NodePos pos in token.Blocks) {
      removed |= _prepared.Remove(pos);
    }
    if (!removed) {
      throw new KeyNotFoundException("Token not found in prepared list.");
    }
    if (token is ConstructRoot root) {
      if (root.IncomingEdgeCount == 0) {
        AddUnreferencedRoot(root);
      }
    }
  }

  public void AddUnreferencedRoot(ConstructRoot root) {
    _unreferencedRoots.Add(root);
  }

  public bool PreparedContains(Token token) {
    foreach (NodePos pos in token.Blocks) {
      if (_prepared.ContainsKey(pos)) {
        return true;
      }
    }
    return false;
  }

  public void AddPending(NodePos pos) {
    Debug.Assert(!_pending.Contains(pos));
    _pending.Add(pos);
  }

  public void AddPrepared(Token token, NodePos refFor) {
    Debug.Assert(token.PendingRef == 0);
    foreach (NodePos pos in token.Blocks) {
      _prepared.Add(pos, token);
    }
    token.AddRef(this, refFor);
  }

  public Token GetOrCreateSource(NodePos source) {
    Debug.Assert(
        _accessor.GetNode(source.Block, source.NodeId, out Node node).IsSource);
    if (Prepared.TryGetValue(source, out Token sourceToken)) {
      return sourceToken;
    }
    return EmitPos(source);
  }
}
