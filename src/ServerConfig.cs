using System;
using System.IO;
using System.Linq;

using Vintagestory.ServerMods.NoObf;

namespace Lambda;

public class ServerConfig {
  // The location where the .v files are written. If this is null, then it
  // defaults to %gamedata%/ModData/lambda.
  public string CoqTmpDir;
  // Path to the coqc binary.
  public string CoqcPath;

  // First resolve the path to coqc. This is the search order (find found
  // instance wins):
  // 1. `CoqcPath` in the config file.
  // 2. COQC environmental variable.
  // 3. coqc or coqc.exe in any of the directories in the PATH environment
  // variable.
  //
  // After the path is resolved, verify that the file exists.
  public void ResolveCoqcPath() {
    if (CoqcPath == null) {
      string var = Environment.GetEnvironmentVariable("COQC");
      if (var != null && var != "") {
        CoqcPath = var;
      }
    }
    if (CoqcPath != null) {
      if (!File.Exists(CoqcPath)) {
        throw new FileNotFoundException(
            $"Cannot find coqc at the config specified path of {CoqcPath}.");
      }
    } else {
      CoqcPath =
          Environment.GetEnvironmentVariable("PATH")
              .Split(Path.PathSeparator)
              .SelectMany(s => new string[] { Path.Combine(s, "coqc"),
                                              Path.Combine(s, "coqc.exe") })
              .Where(File.Exists)
              .FirstOrDefault();
      if (CoqcPath == null) {
        throw new FileNotFoundException(
            "The coqc program is required on the server side. First verify that Coq is installed. If Coq is installed, then either add coqc's path to the PATH environment variable, or set the CoqcPath config option to the location of the file.");
      }
    }
  }
}
