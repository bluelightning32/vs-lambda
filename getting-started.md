This page explains how to play the mod after it is correctly installed. The
client side does not need Coq installed. However, if you are running a server,
or playing in single player mode, Coq must be installed, as explained on the
[readme page](readme.md).

Currently the only item in the mod not related to building functions is the
enhanced pan. The enhanced pan has a 50% greater chance of dropping copper
nuggets (about 22.5% total) and a 50% greater chance of dropping sphalerite
nuggets (3% total). So the early game goal of the mod is to use function
application crafting to create the term `(3, 12, 5)`, which is required in the
crafting grid recipe for the enhanced pan.

## Function application crafting

This is the mod's early game form of crafting. Many of the term objects are
functions, and the application jig allows combining them together to create new
terms.

![Application jig](screenshots/application-jig.png)

The screenshot above shows the application jig on the bottom. It is the main
block used to perfom the function application. The jig is created with stones
and clay, similar to, but with a slightly different recipe than cobblestone.

The mod adds terms items (currently all blue colored in the shape of a spinning
top) which represent functions or arguments to functions. The application jig
combines two terms together to make a new term. The first term must be a
function (called the applicand). The player shift right clicks it into place.
The second term (called the argument) must have a type that matches what the
function accepts. It is also shift right clicked into place. If the argument
has the correct type, then it will stay poking out the top of the jig. If it
has the wrong type, then it will quickly fall out of the jig. Both terms can be
removed with right click.

Early game, you will find functions like these:
* `fst` - Takes an argument in the form (A, B) then returns the first element (the A)
* `snd` - Takes an argument in the form (A, B) then returns the second element (the B)
* `pair 3` - Takes any argument -- let's call it A -- and produces (3, A)
* `S` - Adds 1 to a number
* `pair` - Creates terms of the form (A, B). Put the function in the jig with its first argument, then apply them. It will produce a new function, which remains in the jig. Put the second argument in the jig and apply it.
* `hd` - Returns the first element of a list (something in the form `A :: B :: C :: nil`).
* `nth` - Returns the nth element of a list. It takes 3 arguments:
  * 0-based index of which element to take from the list (the left most element is 0)
  * The list to take the element from
  * A default value to return if the index is out of bounds. The default value must match the type of the rest of the elements in the list. For example, if the list was `4 :: 0 :: 0 :: 5 :: 3 :: nil`, then 12 would be a good default argument, because it is a number like the rest of the elements in the list.

There are two ways to trigger the application. The first way is to the jig with
a hammer twice (hold left click). The terms that were in the jig will
disappear, and instead the result of the application will be in the bottom of
the jig. Right click to take it out.

The first way to trigger the application is only available if you have the metal necessary to make a hammer. So there is another way to trigger it in the early game: drop crafting.
1. Place two grooved cobblestone blocks to the side of the jig and 1 to 2 blocks above. Grooved cobble stone has a similar recipe to regular cobblestone (takes clay and stones).
2. While holding 3 or more sticks in your hotbar, right click the inner edge of one of the grooved cobblestone blocks. The 3 sticks will be placed between the grooved cobblestone blocks as a support for the next block.
3. Right click the stick support with a cobblestone slab. The slab will be placed on top of the sticks.
4. Place 3 sand or gravel blocks on top of the supported cobblestone slab. When the 3rd sand/gravel block is placed, the sticks supporting the slab will break, causing the slab and sand to come falling down. The slab breaks on impact with the application jig. If the application jig contained two terms, the impact of the slab falling will have caused them to get combined.

## Gathering terms

Besides combining terms in the application jig, terms can be gathered in the following ways.
* Harvesting reeds or papyrus
* Killing drifters
* Panning sand/gravel
* Breaking non-branchy leaves
* Harvesting mushrooms
* Breaking boulders (not rocks)
* Harvesting wolves
* Harvesting hares
* Harvesting hyenas

You will need a variety of terms to craft the `(3, 12, 5)` term in the
application jig. However, if you end up with too many or the wrong kinds, you
can cook them into denatured terms in a firepit, then eat them. Although the
denatured terms only provide a little bit of fruit satiety.

## Inscription crafting

The blocks necessary to perform inscription crafting are currently only
available in creative mode. So until the recipes and full directions are
complete, inscription crafting should be considered a preview feature for
adventurous players.

![Swap and solution](screenshots/swap-and-solution.png)

The screenshot above shows solution for an inscription recipe that requires a
`forall A B, A*B -> B*A` function. The input item for the inscription recipe
was placed in the function block. The function block shows the function type on
its label on top.

The blue outline connecting the function block shows the scope of the function.
The function type indicates that the function takes 3 parameters: `A`, `B`, and
`A*B`. So within the function scope are 3 parameter ports. They were placed by
taking the "Out port" and right clicking it onto the blue scope face. There is
also a result port for the function, placed by right clicking an "In port" on a
blue scope face. The result expression of the function was connected to the
result port.

The parameter ports are ordered left-to-right then top-to-bottom (if there were
multiple lines of parameters). The 3rd parameter is connected to the input port
of a match block, and the output of the match block is connected to the result
port for the function.

The match block has one case block. The case block's label shows that it
contains the pair constructor. The `pair` constructor was chosen, because that
is the constructor for `A*B`. The case's scope is shown in orange. The case
scope has two parameter ports to match the two parameters of the `pair`
constructor. It also has a result port.

Inside the case statement are two application blocks. The left-most one gets
its applicand (the function it applies) from its inventory. This is represented
by the label on top of the block. Application blocks need both an applicand and
an argument. The first application block gets its argument from its port on its
backside, which is connected to the `B` parameter of the case scope. The `pair`
constructor takes two arguments. The function is curried, which means after the
first argument is applied, a new function is returned. This partially applied
function is connected to the applicand port of the second application block.
The second application block applies the `A` parameter of the case scope. The
result of the second application has the correct result type, so its result is
connected to the case result.

Right clicking the function block brings up a dialog. When "Inscribe" is
clicked in the dialog, the mod transpiles the block-based visual program into
the following Coq program, then calls `coqc` to verify it is correct, and
matches the type specified by the recipe. If all of that matches, then the
input item is replaced with the output item from the recipe.

Try completing the recipes listed in the
`resources/assets/lambda/recipes/inscription/cobblestone.json` file. The
recipes are listed in order by increasing difficulty.