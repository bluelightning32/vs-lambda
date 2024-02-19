using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class TokenEmissionState : IDisposable {
  private readonly NodeAccessor _accessor;
  // Nodes that have already been created but whose references have not been
  // fully visited yet.
  private readonly Dictionary<NodePos, Token> _prepared = new();
  public IReadOnlyDictionary<NodePos, Token> Prepared { get => _prepared; }
  private readonly List<NodePos> _pending = new();
  private readonly HashSet<NodePos> _pendingSources = new();
  private readonly List<ConstructRoot> _unreferencedRoots = new();
  public IReadOnlyList<ConstructRoot> UnreferencedRoots {
    get => _unreferencedRoots;
  }

  public TokenEmissionState(NodeAccessor accessor) { _accessor = accessor; }

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
      VerifyInvariants();
      NodePos popped = _pending[_pending.Count - 1];
      _pending.RemoveAt(_pending.Count - 1);
      _pendingSources.Remove(popped);
      EmitPos(popped);
      VerifyInvariants();
    }
    Debug.Assert(_pending.Count == 0);
    Debug.Assert(
        _prepared.Count == 0,
        $"Prepared nodes were not cleared, remaining={_prepared.Count} first={_prepared.First()}");
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
      if (_prepared.Remove(pos, out Token removedToken)) {
        Debug.Assert(removedToken == token);
        removed = true;
      }
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

  public bool PreparedContains(NodePos pos, Token token) {
    return _prepared.TryGetValue(pos, out Token contained) &&
           token == contained;
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
    if (_pending.Contains(pos)) {
      Debug.Assert(false);
    }
    _pending.Add(pos);
  }

  public void AddPrepared(NodePos tokenPos, Token token) {
    _prepared.Add(tokenPos, token);
  }

  public void AddPrepared(NodePos tokenPos, Token token, NodePos refFor) {
    AddPrepared(tokenPos, token);
    token.AddRef(this, refFor);
  }

  public Token TryGetSource(NodePos source) {
    Debug.Assert(
        _accessor.GetNode(source.Block, source.NodeId, out Node node).IsSource);
    if (Prepared.TryGetValue(source, out Token sourceToken)) {
      return sourceToken;
    }
    return null;
  }

  public void Dispose() {
    foreach (KeyValuePair<NodePos, Token> p in _prepared) {
      p.Value.Dispose();
    }
    _prepared.Clear();
    foreach (ConstructRoot r in _unreferencedRoots) {
      r.Dispose();
    }
    _unreferencedRoots.Clear();
  }

  public void SaveGraphviz(string testClass, string testName) {
    // To save graphviz files, run the tests with:
    // dotnet test -c Debug --logger:"console;verbosity=detailed" -e GRAPHVIZ=1
    //
    // clang-format off
    //
    // To render the file, use a command like the following:
    // dot -Tsvg test/bin/Debug/net7.0/Lambda.Tests.FunctionTemplateTest.NestedPassthrough.dot -o NestedPassthrough.svg
    //
    // clang-format on
    if (Environment.GetEnvironmentVariable("GRAPHVIZ") == null) {
      return;
    }
    using StreamWriter writer = new($"{testClass}.{testName}.dot");
    SaveGraphviz(testName, writer);
  }

  public void SaveGraphviz(string name, TextWriter writer) {
    GraphvizState graphviz = new(writer);
    graphviz.WriteHeader(name);
    foreach (ConstructRoot root in _unreferencedRoots) {
      graphviz.Add(root);
    }
    graphviz.WriteFooter();
  }

  public Token AddPort(NodePos source, BlockPos childPos, NodeTemplate child) {
    BlockNodeTemplate template = _accessor.GetBlock(
        source.Block, out Node[] nodes, out string inventoryTerm);
    return ((IAcceptScopePort) template)
        .AddPort(this, source, nodes, inventoryTerm, childPos, child);
  }

  public Case AddCase(NodePos source, NodePos childMatchPos, int childScopeId,
                      BlockFacing face, string inventoryTerm) {
    BlockNodeTemplate template = _accessor.GetBlock(
        source.Block, out Node[] nodes, out string matchInventory);
    return ((MatchTemplate) template)
        .AddCase(this, source, nodes, childMatchPos, childScopeId, face,
                 inventoryTerm);
  }

  public void VerifyInvariants() {
    HashSet<NodePos> sourceHasPending = new();
    HashSet<NodePos> pendingSet = new();
    foreach (NodePos pos in _pending) {
      if (!pendingSet.Add(pos)) {
        throw new Exception(
            $"The pending list contains multiple copies of {pos}.");
      }
      BlockNodeTemplate template = _accessor.GetBlock(
          pos.Block, out Node[] nodes, out string inventoryTerm);
      NodePos source = nodes[pos.NodeId].Source;
      if (!nodes[pos.NodeId].IsConnected()) {
        source = pos;
      }
      if (!_prepared.ContainsKey(source) && !_pendingSources.Contains(source)) {
        throw new Exception(
            $"Pending node {pos} does not have a prepared source at {source}.");
      }
      sourceHasPending.Add(source);
    }
    foreach (NodePos pos in _pendingSources) {
      if (!pendingSet.Contains(pos)) {
        throw new Exception($"{pos} is in _pendingSources but not _pending.");
      }
    }
    foreach (KeyValuePair<NodePos, Token> entry in _prepared) {
      if (entry.Value.PendingRef == 0) {
        throw new Exception($"Prepared node {entry.Key} has 0 references.");
      }
      foreach (NodePos refHolder in entry.Value.PendingRefLocations) {
        BlockNodeTemplate template = _accessor.GetBlock(
            refHolder.Block, out Node[] nodes, out string inventoryTerm);
        NodePos source = nodes[refHolder.NodeId].Source;
        if (!nodes[refHolder.NodeId].IsConnected()) {
          source = refHolder;
        }
        if (!sourceHasPending.Contains(source)) {
          throw new Exception(
              $"Prepared node {entry.Key} is referenced by {refHolder}, which has source {source}, but the source has no pending children.");
        }
      }
    }
  }

  public void MaybeAddPendingSource(NodePos sourcePos) {
    if (Prepared.TryGetValue(sourcePos, out Token portSource)) {
      // If the port source is already in the prepared dict, then the source
      // or its connectors should already be pending.
      Debug.Assert(portSource.PendingRef > 0);
      return;
    }
    if (_pendingSources.Add(sourcePos)) {
      AddPending(sourcePos);
    }
  }

  public void MaybeAddPendingSource(NodePos child, Node[] nodes) {
    NodePos childSource = nodes[child.NodeId].Source;
    if (!nodes[child.NodeId].IsConnected()) {
      childSource = child;
    }
    MaybeAddPendingSource(childSource);
  }
}
