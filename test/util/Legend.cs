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
          class: 'CaseTemplate',
          face: 'south',
          nodes: [
            {
              name: 'match',
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            },
            {
              name: 'scope',
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source']
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

  public void AddMatchIn(char key, string term) {
    BlockNodeTemplate template =
        _manager.ParseBlockNodeTemplate(JsonObject.FromJson(@"
        {
          class: 'MatchInTemplate',
          face: 'south',
          nodes: [
            {
              name: 'match',
              network: 'match',
              edges: ['north-center', 'east-center', 'south-center', 'west-center']
            },
            {
              name: 'scope',
              network: 'scope',
              edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source']
            }
          ],
          ports: [
            {
              name: 'type',
              network: 'term',
              directions: ['direct-in'],
              faces: [],
              inventory: {
                requireTerm: true,
                requireTypeFamily: true,
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
          class: 'AppTemplate',
          nodes: [
            {
              name: 'output',
              network: 'term',
              edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source']
            }
          ],
        }"),
        (int)PortDirection.DirectOut
            << (BlockFacing.EAST.Index * Manager.OccupiedPortsBitsPerFace),
        0);

    Dict.Add(key, new(template, term));
  }
}