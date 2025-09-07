# ModShardUnpacker

A CLI tool to unpack a sml file into a mod code source.

## What will I get from this tool ?

MSU will unpack all auxiliary `.png`, `.lua`, `.gml` and `asm` files as well as all `.cs` files used to run the mod.

## How can I install it?

1. Install the latest [.NET Core SDK](https://dot.net).
2. Run `dotnet tool install --global ModShardUnpacker`.

## How can I use it?

### Using the CLI

To unpack the content of `my_mod.sml` and export the results in the folder `PATH/TO/EXPORT`, run `msu -n my_mod.sml -o PATH/TO/EXPORT`.
