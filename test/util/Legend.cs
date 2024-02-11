using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

public class Legend {
  public readonly Dictionary<char, Tuple<BlockNodeTemplate, string>> Dict =
      new();
  private readonly Manager _manager;

  public Legend(Manager manager) { _manager = manager; }

  public void AddPuzzle(char key, string puzzleType) {
    BlockNodeTemplate template =
        _manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'FunctionTemplate',
          face: 'south',
          nodes: [
            {
              network: 'scope',
              name: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source']
            }
          ],
          ports: [
            {
              name: 'output',
              network: 'term',
              directions: ['direct-out'],
              inventory: {
                requireTerm: false,
                hidePerishRate: false
              }
            }
          ]
        }"),
                                        0, 0);
    Dict.Add(key, new(template, puzzleType));
  }

  public void AddCase(char key, string term) {
    BlockNodeTemplate template =
        _manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          nodes: [
            {
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
              name: 'constructor',
              network: 'term',
              directions: ['direct-in'],
              faces: [],
              inventory: {
                requireTerm: true,
                requireConstructor: true,
                maxSlotStackSize: 1,
              }
            }
          ]
        }"),
                                        0, 0);
    Dict.Add(key, new(template, term));
  }

  public void AddConstant(char key, string term) {
    BlockNodeTemplate template = _manager.ParseBlockNodeTemplate(
        JsonObject.FromJson(@"
        {
          nodes: [],
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
        0,
        ((int)PortDirection.DirectOut
         << (BlockFacing.EAST.Index * Manager.OccupiedPortsBitsPerFace)));
    Dict.Add(key, new(template, term));
  }
}