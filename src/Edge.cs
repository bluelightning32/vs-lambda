using System.Runtime.Serialization;

using Vintagestory.API.MathTools;

namespace LambdaFactory;

public enum Edge {
  [EnumMember(Value = "north-right")] NorthRight = 0,
  [EnumMember(Value = "north-up")] NorthUp = 1,
  [EnumMember(Value = "north-left")] NorthLeft = 2,
  [EnumMember(Value = "north-down")] NorthDown = 3,
  [EnumMember(Value = "north-center")] NorthCenter = 4,
  [EnumMember(Value = "east-right")] EastRight = 5,
  [EnumMember(Value = "east-up")] EastUp = 6,
  [EnumMember(Value = "east-left")] EastLeft = 7,
  [EnumMember(Value = "east-down")] EastDown = 8,
  [EnumMember(Value = "east-center")] EastCenter = 9,
  [EnumMember(Value = "south-right")] SouthRight = 10,
  [EnumMember(Value = "south-up")] SouthUp = 11,
  [EnumMember(Value = "south-left")] SouthLeft = 12,
  [EnumMember(Value = "south-down")] SouthDown = 13,
  [EnumMember(Value = "south-center")] SouthCenter = 14,
  [EnumMember(Value = "west-right")] WestRight = 15,
  [EnumMember(Value = "west-up")] WestUp = 16,
  [EnumMember(Value = "west-left")] WestLeft = 17,
  [EnumMember(Value = "west-down")] WestDown = 18,
  [EnumMember(Value = "west-center")] WestCenter = 19,
  [EnumMember(Value = "up-right")] UpRight = 20,
  [EnumMember(Value = "up-up")] UpUp = 21,
  [EnumMember(Value = "up-left")] UpLeft = 22,
  [EnumMember(Value = "up-down")] UpDown = 23,
  [EnumMember(Value = "up-center")] UpCenter = 24,
  [EnumMember(Value = "down-right")] DownRight = 25,
  [EnumMember(Value = "down-up")] DownUp = 26,
  [EnumMember(Value = "down-left")] DownLeft = 27,
  [EnumMember(Value = "down-down")] DownDown = 28,
  [EnumMember(Value = "down-center")] DownCenter = 29,
  [EnumMember(Value = "source")] Source = 30,
  [EnumMember(Value = "unknown")] Unknown = 100,
}

static class EdgeExtension {
  public static BlockFacing GetFace(this Edge edge) {
    return edge switch {
      Edge.NorthRight or Edge.NorthUp or Edge.NorthLeft or
          Edge.NorthDown or Edge.NorthCenter => BlockFacing.NORTH,
      Edge.EastRight or Edge.EastUp or Edge.EastLeft or Edge.EastDown or
          Edge.EastCenter => BlockFacing.EAST,
      Edge.SouthRight or Edge.SouthUp or Edge.SouthLeft or
          Edge.SouthDown or Edge.SouthCenter => BlockFacing.SOUTH,
      Edge.WestRight or Edge.WestUp or Edge.WestLeft or Edge.WestDown or
          Edge.WestCenter => BlockFacing.WEST,
      Edge.UpRight or Edge.UpUp or Edge.UpLeft or Edge.UpDown or
          Edge.UpCenter => BlockFacing.UP,
      Edge.DownRight or Edge.DownUp or Edge.DownLeft or Edge.DownDown or
          Edge.DownCenter => BlockFacing.DOWN,
      _ => null
    };
  }

  public static Edge GetOpposite(this Edge edge) {
    return edge switch { Edge.NorthRight => Edge.SouthLeft,
                         Edge.EastRight => Edge.WestLeft,
                         Edge.SouthRight => Edge.NorthLeft,
                         Edge.WestRight => Edge.EastLeft,

                         Edge.NorthLeft => Edge.SouthRight,
                         Edge.EastLeft => Edge.WestRight,
                         Edge.SouthLeft => Edge.NorthRight,
                         Edge.WestLeft => Edge.EastRight,

                         Edge.NorthUp => Edge.SouthUp,
                         Edge.EastUp => Edge.WestUp,
                         Edge.SouthUp => Edge.NorthUp,
                         Edge.WestUp => Edge.EastUp,

                         Edge.NorthDown => Edge.SouthDown,
                         Edge.EastDown => Edge.WestDown,
                         Edge.SouthDown => Edge.NorthDown,
                         Edge.WestDown => Edge.EastDown,

                         Edge.UpUp => Edge.DownUp,
                         Edge.UpDown => Edge.DownDown,
                         Edge.UpLeft => Edge.DownRight,
                         Edge.UpRight => Edge.DownLeft,

                         Edge.DownUp => Edge.UpUp,
                         Edge.DownDown => Edge.UpDown,
                         Edge.DownLeft => Edge.UpRight,
                         Edge.DownRight => Edge.UpLeft,

                         Edge.NorthCenter => Edge.SouthCenter,
                         Edge.EastCenter => Edge.WestCenter,
                         Edge.SouthCenter => Edge.NorthCenter,
                         Edge.WestCenter => Edge.EastCenter,
                         Edge.UpCenter => Edge.DownCenter,
                         Edge.DownCenter => Edge.UpCenter,

                         _ => Edge.Unknown };
  }
}