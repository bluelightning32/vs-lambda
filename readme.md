# Lambda - Coq logic puzzle mod

This is a [mod](https://mods.vintagestory.at/lambda) for the Vintage Story video game. The mod introduces two new crafting mechanics:
* Applying functions in the application jig
* Inscribing, where an item is put into the function block, and it shows a
  program the player must create to transform the item into the recipe output.
  The program is built by connecting more blocks to the function block, which
  act as a visual programming language that compiles into Coq.

To learn how to play the mod after it is installed, see the [getting started guide](getting-started.md).

![Swap and solution](screenshots/swap-and-solution.png)

The screenshot above shows solution for an inscription recipe that requires a
`forall A B, A*B -> B*A` function. The solution shown above compiles into this program, which the mod passes to Coq to type check.
```
Definition puzzle: (forall A B,
A*B -> B*A):=
fun A B ab =>
  match ab with
  | pair parameter_511888_113_512015_0_2 parameter_511888_113_512013_0_2 =>
    pair parameter_511888_113_512013_0_2 parameter_511888_113_512015_0_2
  end.
```

## Locating coqc

The server side of the mod needs to run the `coqc` program that comes from a
Coq installation. `coqc` is not called from the client side of the mod. For
developers, some of the unit tests also invoke that program. The tests and mod
search the following locations for coqc:
1. The location specified with the CoqcPath option in serverconfig.json (mod
   only).
2. The COQC environment variable. If used, this should be the complete filename
   of coqc.
3. `coqc` or `coqc.exe` in all folders in the `PATH` environment variable.

## Security

Coq code is only run on the Vintage Story server side. So there is no security
risk to Vintage Story game clients, beyond the usual risk of running a mod that
has not gone through an official security review.

Players can create new Coq programs by appending existing ones together. So the
kinds of programs that can be created in survival mode are very constrained.
However, in creative mode, a player could directly edit the attribute of the
genericterm item to create an arbitrary Coq program.

Since the Coq language was designed for proving math statements, there are
relatively few ways to escape out and arbitrarily access the hosting computer.
To block those few remaining ways, the mod always runs Coq code through a
sanitizer before executing it. The sanitizer blocks non-ascii characters and
only allows a few Coq commands to be run. Here are the known Coq escapes. These
are all blocked by the sanitizer (see `CoqSanitizerTest`).
* [Drop](https://coq.inria.fr/doc/V8.19.0/refman/proof-engine/vernacular-commands.html?highlight=drop#coq:cmd.Drop) - allows running arbitrary OCaml code
* [Redirect](https://coq.inria.fr/doc/V8.19.0/refman/proof-engine/vernacular-commands.html?highlight=drop#coq:cmd.Drop) - allows writing to any filename that ends with ".out".
* [Cd](https://coq.inria.fr/doc/V8.18.0/refman/proof-engine/vernacular-commands.html#coq:cmd.Cd) - changes the current directory. It would let the player probe which directories exist on the file system.
* [Ltac2 @ external](https://coq.inria.fr/doc/V8.18.0/refman/proof-engine/ltac2.html#coq:cmd.Ltac2-external) - binds an ltac function defined in ocaml to ltac2. These should be safe, but the command is blocked to be safe, in case there was an ltac2 function that was purposefully not imported to Coq.
* [Locate File](https://coq.inria.fr/doc/V8.18.0/refman/proof-engine/ltac2.html#coq:cmd.Ltac2-external) - allows probing the filesystem to check whether files exist.
* [Extraction module](https://coq.inria.fr/doc/V8.18.0/refman/addendum/extraction.html#generating-ml-code) - has commands that write new files on the file system in arbitrary directories.

Server owners that are especially worried about security are encouraged to host
the game on Linux and wrap coqc with the unshare program to block its access to
the rest of the computer.

## Missing features

This mod is in early access. It is still missing the following features:
1. A primitive destruct block to introduce the player to the concept of `match` without having to build a full match with cases in a function scope. The destruct block would also let the player unlock the rest of the constructor terms (most can only be accessed in creative mode).
2. Handbook entries or maybe a wiki. For now, look at the [getting started guide](getting-started.md) to see how to use the application jig. If you're feeling adventurous, look up the inscription recipes in the source code, and try solving them in creative mode.
3. Recipes to obtain the blocks to perform inscription crafting (can only be accessed in creative mode currently). The application jig blocks have recipes.
5. Balanced inscription recipes.
6. A few reward items (maybe a slightly more powerful axe?) to motivate the player to solve the puzzles.
7. Puzzle/function block breaks the solution blocks upon a successful inscription, to stop the player from spamming the inscription recipe, and to force the player to learn through some repetition.
8. A way to inspect the type of ports while building a function.
9. Better translation of Coq errors into block locations.
10. Inscription sound effects.
11. Puzzles that check not just the output type but the term produced by the function.
12. Fixpoints.

## Building

The `VINTAGE_STORY` environment variable must be set before loading the
project. It should be set to the install location of Vintage Story (the
directory that contains VintagestoryLib.dll).

A Visual Studio Code workspace is included. The mod can be built through it or
from the command line.

### Release build from command line

This will produce a zip file in a subfolder of `bin/Release`.
```
dotnet build -c Release
```

### Debug build from command line

This will produce a zip file in a subfolder of `bin/Debug`.
```
dotnet build -c Debug
```

### Run unit tests

```
dotnet test -c Debug --logger:"console;verbosity=detailed"
```

### Run unit tests with graphviz output

```
dotnet test -c Debug --logger:"console;verbosity=detailed" -e GRAPHVIZ=1
```
