using Lambda.Network;

using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

public class TestBlockNodeTemplates {
  public BlockNodeTemplate FourWay;
  public BlockNodeTemplate NS;
  public BlockNodeTemplate FourWaySource;
  // Connects match network in the 4 cardinal directions.
  public BlockNodeTemplate MatchConnector;
  // Connects match and scope network in the 4 cardinal directions.
  public BlockNodeTemplate ScopeMatchConnector;
  // Connects term network in the 4 cardinal directions.
  public BlockNodeTemplate Wire;
  // Connects term network on the north and south faces. It also connects the
  // term network on the east and west faces. This is like a north-south wire
  // and a east-west wire that are placed on top of each other but do not
  // connect.
  public BlockNodeTemplate WireCross;
  // Scope block with an in port on the south face. It also connects the match
  // and scope networks in the 4 cardinal directions.
  public BlockNodeTemplate InPort;
  // Scope block with an out port on the west face. It also connects the match
  // and scope networks in the 4 cardinal directions.
  public BlockNodeTemplate OutPort;
  // Application with the applicand (function) port on the west face, argument
  // port on the north face, and output port on the east face.
  public BlockNodeTemplate App;
  // Match block with the input port the west face and the output port on the
  // east face. It is a source for the match network in the 4 cardinal
  // directions.
  public BlockNodeTemplate Match;

  private readonly Manager _manager;

  public TestBlockNodeTemplates(Manager manager) {
    _manager = manager;
    FourWay = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            }
          ]
        }"),
                                             0, 0);

    NS = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              network: 'scope',
              edges: ['north-center', 'south-center']
            }
          ]
        }"),
                                        0, 0);

    FourWaySource = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
      {
        class: 'ScopeTemplate',
        nodes: [
          {
            network: 'scope',
            edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source'],
            sourceScope: 'function'
          }
        ]
      }"),
                                                   0, 0);

    MatchConnector = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          nodes: [
            {
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            }
          ]
        }"),
                                                    0, 0);

    ScopeMatchConnector = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            },
            {
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            }
          ]
        }"),
                                                         0, 0);

    Wire = manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              network: 'term'
            }
          ]
        }"),
        0,
        BlockFacing.NORTH.Flag | BlockFacing.EAST.Flag |
            BlockFacing.SOUTH.Flag | BlockFacing.WEST.Flag);

    WireCross = manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              network: 'term',
              edges: ['north-center', 'south-center']
            },
            {
              network: 'term',
              edges: ['east-center', 'west-center']
            }
          ]
        }"),
                                               0, 0);

    // An in port brings data into the construct. Within the construct, it is an
    // output port.
    InPort = manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              name: 'scope',
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            },
            {
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            }
          ],
          ports: [
            {
              name: 'parameter',
              network: 'term',
              parent: 'scope',
              directions: ['direct-in', 'direct-out'],
              faces: ['south']
            }
          ]
        }"),
        (int)PortDirection.DirectOut
            << (BlockFacing.SOUTH.Index * Manager.OccupiedPortsBitsPerFace),
        0);

    // An out port brings data out of the construct. Within the construct, it is
    // an input port.
    OutPort = manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          class: 'ScopeTemplate',
          nodes: [
            {
              name: 'scope',
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            },
            {
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            }
          ],
          ports: [
            {
              name: 'parameter',
              network: 'term',
              parent: 'scope',
              directions: ['direct-in', 'direct-out'],
              faces: ['west']
            }
          ]
        }"),
        (int)PortDirection.DirectIn
            << (BlockFacing.WEST.Index * Manager.OccupiedPortsBitsPerFace),
        0);

    App = manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          ports: [
            {
              name: 'applicand',
              network: 'term',
              directions: ['direct-in'],
              faces: ['west'],
              inventory: {
                requireTerm: true,
                maxSlotStackSize: 1,
              }
            },
            {
              name: 'argument',
              network: 'term',
              directions: ['direct-in'],
              faces: ['north'],
              inventory: {
                requireTerm: true,
                maxSlotStackSize: 1,
              }
            },
            {
              name: 'output',
              network: 'term',
              directions: ['direct-out'],
              faces: ['east']
            }
          ]
        }"),
        ((int)PortDirection.DirectIn
         << (BlockFacing.WEST.Index * Manager.OccupiedPortsBitsPerFace)) |
            ((int)PortDirection.DirectIn
             << (BlockFacing.NORTH.Index * Manager.OccupiedPortsBitsPerFace)) |
            ((int)PortDirection.DirectOut
             << (BlockFacing.EAST.Index * Manager.OccupiedPortsBitsPerFace)),
        0);

    Match = manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          nodes: [
            {
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source']
            },
          ],
          ports: [
            {
              name: 'input',
              network: 'term',
              directions: ['direct-in'],
              faces: ['west']
            },
            {
              name: 'output',
              network: 'term',
              directions: ['direct-out'],
              faces: ['east']
            }
          ]
        }"),
        ((int)PortDirection.DirectIn
         << (BlockFacing.WEST.Index * Manager.OccupiedPortsBitsPerFace)) |
            ((int)PortDirection.DirectOut
             << (BlockFacing.EAST.Index * Manager.OccupiedPortsBitsPerFace)),
        0);
  }

  // Adds the standard blocks to the legend.
  public Legend CreateLegend() {
    Legend legend = new(_manager);
    legend.Dict.Add(' ', null);
    legend.Dict.Add('.', new(MatchConnector, null));
    legend.Dict.Add('#', new(ScopeMatchConnector, null));
    legend.Dict.Add('+', new(Wire, null));
    legend.Dict.Add('*', new(WireCross, null));
    legend.Dict.Add('i', new(InPort, null));
    legend.Dict.Add('o', new(OutPort, null));
    legend.Dict.Add('A', new(App, null));
    legend.Dict.Add('M', new(Match, null));
    return legend;
  }
}