using System.Runtime.Serialization;

using Vintagestory.API.MathTools;

namespace LambdaFactory;

public enum Edge {
  [EnumMember(Value = "unknown")] Unknown = 0,
  [EnumMember(Value = "north-right")] NorthRight = 1,
  [EnumMember(Value = "north-up")] NorthUp = 2,
  [EnumMember(Value = "north-left")] NorthLeft = 3,
  [EnumMember(Value = "north-down")] NorthDown = 4,
  [EnumMember(Value = "north-center")] NorthCenter = 5,
  [EnumMember(Value = "east-right")] EastRight = 6,
  [EnumMember(Value = "east-up")] EastUp = 7,
  [EnumMember(Value = "east-left")] EastLeft = 8,
  [EnumMember(Value = "east-down")] EastDown = 9,
  [EnumMember(Value = "east-center")] EastCenter = 10,
  [EnumMember(Value = "south-right")] SouthRight = 11,
  [EnumMember(Value = "south-up")] SouthUp = 12,
  [EnumMember(Value = "south-left")] SouthLeft = 13,
  [EnumMember(Value = "south-down")] SouthDown = 14,
  [EnumMember(Value = "south-center")] SouthCenter = 15,
  [EnumMember(Value = "west-right")] WestRight = 16,
  [EnumMember(Value = "west-up")] WestUp = 17,
  [EnumMember(Value = "west-left")] WestLeft = 18,
  [EnumMember(Value = "west-down")] WestDown = 19,
  [EnumMember(Value = "west-center")] WestCenter = 20,
  [EnumMember(Value = "up-right")] UpRight = 21,
  [EnumMember(Value = "up-up")] UpUp = 22,
  [EnumMember(Value = "up-left")] UpLeft = 23,
  [EnumMember(Value = "up-down")] UpDown = 24,
  [EnumMember(Value = "up-center")] UpCenter = 25,
  [EnumMember(Value = "down-right")] DownRight = 26,
  [EnumMember(Value = "down-up")] DownUp = 27,
  [EnumMember(Value = "down-left")] DownLeft = 28,
  [EnumMember(Value = "down-down")] DownDown = 29,
  [EnumMember(Value = "down-center")] DownCenter = 30,
  [EnumMember(Value = "source")] Source = 31,
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

  public static Edge GetFaceCenter(BlockFacing face) {
    return face.Index switch { BlockFacing.indexNORTH => Edge.NorthCenter,
                               BlockFacing.indexEAST => Edge.EastCenter,
                               BlockFacing.indexSOUTH => Edge.SouthCenter,
                               BlockFacing.indexWEST => Edge.WestCenter,
                               BlockFacing.indexUP => Edge.UpCenter,
                               BlockFacing.indexDOWN => Edge.DownCenter,
                               _ => Edge.Unknown };
  }

  public static bool IsFaceCenter(this Edge edge) {
    return edge switch { Edge.NorthCenter => true,
                         Edge.EastCenter => true,
                         Edge.SouthCenter => true,
                         Edge.WestCenter => true,
                         Edge.UpCenter => true,
                         Edge.DownCenter => true,
                         _ => false };
  }
}