using System.Globalization;
using System.Text;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

try
{
    var rootCommand = new RootCommand("Converts a .csv file to a Steam Community markdown table.");

    var inputFileArg = new Argument<FileInfo>();
    rootCommand.AddArgument(inputFileArg);

    var fileOutOption = new Option<FileInfo?>(name: "--file-out", description: "Save the output to a specified file.");
    rootCommand.AddOption(fileOutOption);

    var autoOutOption = new Option<bool>(name: "--auto-out", description: "Save the output to a file with the same name and directory as the input file, except with the extension \".steam_md.txt\".");
    rootCommand.AddOption(autoOutOption);

    var clipboardOutOption = new Option<bool>(name: "--clipboard-out", description: "Copy the output to the clipboard.");
    rootCommand.AddOption(clipboardOutOption);

    var headerRowOption = new Option<int>(name: "--header-row", description: "The row to apply \"th\" tags to instead of \"td\". Default is 1. Use 0 for none.", getDefaultValue: () => 1);
    rootCommand.AddOption(headerRowOption);

    var headerColumnOption = new Option<int>(name: "--header-column", description: "The column to apply \"th\" tags to instead of \"td\". Default is 0. Use 0 for none.", getDefaultValue: () => 0);
    rootCommand.AddOption(headerColumnOption);

    rootCommand.SetHandler((FileInfo inputFile, FileInfo? fileOut, bool autoOut, bool clipboardOut, int headerRow, int headerColumn) =>
    {
        StringSheet inputSheet;
        using (var streamReader = inputFile.OpenText())
        {
            using (var csvReader = new CsvHelper.CsvReader(streamReader, CultureInfo.InvariantCulture))
            {
                inputSheet = new StringSheet(csvReader);
            }
        }

        // Default to --auto-out if no output option was provided.
        if (
        (fileOut == null)
        && !autoOut
        && !clipboardOut
        )
        {
            Console.WriteLine("No output option provided. Press 'a' for --auto-out, 'c' for --clipboard-out, or 'esc' to abort.");
            for (bool continueLoop = true; continueLoop;)
            {
                continueLoop = false;
                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.A: autoOut = true; break;
                    case ConsoleKey.C: clipboardOut = true; break;
                    case ConsoleKey.Escape: return;
                    default: continueLoop = true; break;
                }
            }
        }

        // Generate results
        string resultContents = ConvertStringSheetToSteamMarkdown(inputSheet, headerRow, headerColumn);

        if (fileOut != null)
        {
            using (var stream = fileOut.CreateText())
            {
                stream.Write(resultContents);
            }
        }

        if (autoOut)
        {
            string inputFilePath = inputFile.FullName;

            string autoOutPath = Path.Join(Path.GetDirectoryName(inputFilePath), Path.GetFileNameWithoutExtension(inputFilePath) + ".steam_md.txt");
            File.WriteAllText(autoOutPath, resultContents);
        }

        if (clipboardOut)
        {
            TextCopy.ClipboardService.SetText(resultContents);
        }

    }, inputFileArg, fileOutOption, autoOutOption, clipboardOutOption, headerRowOption, headerColumnOption);

    Exception? cmdException = null;
    var parser = new CommandLineBuilder(rootCommand)
        .UseExceptionHandler((e, invocationContext) =>
        {
            cmdException = e;
        })
        .Build();
    parser.Invoke(args);

    if (cmdException != null)
    {
        LogExceptionAndPromptToQuit(cmdException);
    }
    else
    {
        Console.WriteLine("Done.");
    }
}
catch (Exception e)
{
    LogExceptionAndPromptToQuit(e);
}

void LogExceptionAndPromptToQuit(Exception e)
{
    string errorMsg;

    switch (e)
    {
        // For exceptions we anticipate the user to trip due to misuse, just show the simplified message.
        case InvalidDataException invalidDataException:
            {
                errorMsg = e.Message;
            } break;
        // For all other exceptions, give a stack trace.
        default:
            {
                errorMsg = e.ToString();
            } break;
    }

    Console.Write("Error: ");
    Console.WriteLine(errorMsg);
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
    Environment.Exit(1);
}

string ConvertStringSheetToSteamMarkdown(StringSheet stringSheet, int headerRow, int headerColumn)
{
    StringBuilder outputContents = new();
    SteamMarkdownBuilder outputTableBuilder = new(outputContents);

    using (outputTableBuilder.NewTag("table", true))
    {

        List<IReadOnlyList<string>> allRows = new();
        allRows.Add(stringSheet.header);
        allRows.AddRange(stringSheet.rows);

        int tableRow = 0;

        foreach (var row in allRows)
        {
            using (outputTableBuilder.NewTag("tr"))
            {
                ++tableRow;
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    int tableColumn = columnIndex + 1;
                    bool isHeader = ((tableRow == headerRow) || (columnIndex == headerColumn));

                    string? value = row[columnIndex];
                    using (outputTableBuilder.NewTag(isHeader ? "th" : "td", false)) { outputTableBuilder.Append(value); }
                }
            }
        }
    }

    return outputContents.ToString();
}
