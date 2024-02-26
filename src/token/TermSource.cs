using System.Collections.Generic;

using Lambda.Network;

namespace Lambda.Token;

public abstract class TermSource : Token {
  private readonly List<NodePos> _termConnectors = new();
  public override IReadOnlyList<NodePos> TermConnectors => _termConnectors;

  public TermSource(string name) : base(name) {}

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
