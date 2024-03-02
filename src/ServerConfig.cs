using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Lambda;

public class ServerConfig {
  // The location where the .v files are written. If this is null, then it
  // defaults to %gamedata%/ModData/lambda.
  public string CoqTmpDir;
  // Path to the coqc binary.
  public string CoqcPath;
}
