using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace ModShardUnpacker;
internal static class Program
{
    static async Task Main(string[] args)
    {
        Option<string> nameOption = new("--name")
        {
            Description = "Name of the output.",
            IsRequired = false
        };
        nameOption.AddAlias("-n");

        Option<string?> outputOption = new("--output")
        {
            Description = "Output folder."
        };
        outputOption.AddAlias("-o");
        outputOption.SetDefaultValue(null);

        RootCommand rootCommand = new("A CLI tool to pack mod source from MSL.")
        {
            nameOption,
            outputOption,
        };

        rootCommand.SetHandler(MainOperations.MainCommand, nameOption, outputOption);

        CommandLineBuilder commandLineBuilder = new(rootCommand);

        commandLineBuilder.AddMiddleware(async (context, next) =>
        {
            await next(context);
        });

        commandLineBuilder.UseDefaults();
        Parser parser = commandLineBuilder.Build();

        await parser.InvokeAsync(args);
    }
}
