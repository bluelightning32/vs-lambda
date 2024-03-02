using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lambda.Token;

// This is tracks the current state of finding a common ancestor for construct
// roots that have multiple incoming edges. These objects are no longer
// referenced after multiuse scoping is done.
public class AnchorPoint {
  // These construct roots have been fully visited and are ready to be anchored
  // at this layer.
  private List<ConstructRoot> _ready = null;
  public IReadOnlyList<ConstructRoot> Ready { get => _ready; }
  public int ReadyCount { get => _ready != null ? _ready.Count : 0; }

  // If this is non-null, that indicates that ready constructs should be added
  // to the replacement's ready list instead of this tracker's ready list.
  // `Replacement` is only modified once.
  private AnchorPoint _replacement = null;
  // This is how many constructs are pointing to this tracker.
  private int _references = 0;

  public AnchorPoint CreateSubtracker() {
    if (_references == 0) {
      // Reuse this tracker since nothing refers to it yet.
      return this;
    }
    return new();
  }

  public void ReleaseSubtracker(AnchorPoint child) {
    child._references -= child.ReadyCount;
    child._ready?.Clear();
    if (child != this && child._references > 0) {
      // The parent was not reused as a subtracker. So now merge them through
      // the replacement field.
      child._replacement = this;
      _references += child._references;
    }
  }

  public void AddReady(ConstructRoot c) {
    // Find the final replacement
    AnchorPoint final = this;
    while (final._replacement != null) {
      final = final._replacement;
    }
    // Update all of the nodes on the path to now point directly to the final
    // replacement. This will speed up finding the final node in the future for
    // anything else that references the old trackers.
    AnchorPoint t = this;
    while (t != final) {
      AnchorPoint copy = t;
      t = t._replacement;
      copy._replacement = final;
    }
    // Now mark the construct root as ready in the final tracker.
    final._ready ??= new();
    final._ready.Add(c);
  }

  public void AddReference() { ++_references; }

  public void Done() {
    Debug.Assert(_references == ReadyCount);
    _ready?.Clear();
  }
}