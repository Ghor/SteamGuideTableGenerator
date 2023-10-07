using System.Globalization;
using System.Text;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

try
{
    var rootCommand = new RootCommand("Converts a .csv file to a Steam Community markdown table.");

    var inputFileArg = new Argument<FileInfo>().ExistingOnly();
    inputFileArg.Arity = ArgumentArity.ExactlyOne;
    rootCommand.AddArgument(inputFileArg);

    var fileOutOption = new Option<FileInfo?>(name: "--file-out", description: "Save the output to a specified file.").LegalFilePathsOnly();
    rootCommand.AddOption(fileOutOption);

    var autoOutOption = new Option<bool>(name: "--auto-out", description: "Save the output to a file with the same name and directory as the input file, except with the extension \".steam_md.txt\".");
    rootCommand.AddOption(autoOutOption);

    var clipboardOutOption = new Option<bool>(name: "--clipboard-out", description: "Copy the output to the clipboard.");
    rootCommand.AddOption(clipboardOutOption);

    var headerRowOption = new Option<int>(name: "--header-row", description: "The row to apply \"th\" tags to instead of \"td\". Use 0 for none.", getDefaultValue: () => 1);
    rootCommand.AddOption(headerRowOption);

    var headerColumnOption = new Option<int>(name: "--header-column", description: "The column to apply \"th\" tags to instead of \"td\". Use 0 for none.", getDefaultValue: () => 0);
    rootCommand.AddOption(headerColumnOption);

    var wrapLimitOption = new Option<int>(name: "--wrap-limit", description: "The maximum number of characters a single line can have before being forced to the next line. This is useful for tables whose contents are too wide.", getDefaultValue: () => 0);
    rootCommand.AddOption(wrapLimitOption);

    rootCommand.SetHandler((FileInfo inputFile, FileInfo? fileOut, bool autoOut, bool clipboardOut, int headerRow, int headerColumn, int wrapLimit) =>
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
        string resultContents = ConvertStringSheetToSteamMarkdown(inputSheet, headerRow, headerColumn, wrapLimit);

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

    }, inputFileArg, fileOutOption, autoOutOption, clipboardOutOption, headerRowOption, headerColumnOption, wrapLimitOption);

    Exception? cmdException = null;
    var parser = new CommandLineBuilder(rootCommand)
        .UseDefaults()
        .UseExceptionHandler((e, invocationContext) =>
        {
            cmdException = e;
        })
        .Build();

    var parseResult = parser.Parse(args);
    int cmdResult = parseResult.Invoke();

    // Display any errors that occurred during invocation.
    if (cmdException != null)
    {
        LogException(cmdException);
    }

    if ((cmdResult != 0) || (cmdException != null))
    {
        PromptToQuit();
    }
    else
    {
        Console.WriteLine("Done.");
    }
}
catch (Exception e)
{
    LogException(e);
    PromptToQuit();
}

void LogException(Exception e)
{
    string errorMsg;
    switch (e)
    {
        // For exceptions we anticipate the user to trip due to misuse, just show the simplified message.
        case CsvHelper.BadDataException:
            {
                errorMsg = "Invalid or malformed csv file.";
            }
            break;
        case InvalidDataException:
            {
                errorMsg = e.Message;
            }
            break;
        // For all other exceptions, give a stack trace.
        default:
            {
                errorMsg = e.ToString();
            }
            break;
    }

    Console.Write("Error: ");
    Console.WriteLine(errorMsg);
}


// Pauses until user input so they have enough time to see the error message.
void PromptToQuit()
{
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
    Environment.Exit(1);
}

string WrapString(string str, int wrapLimit)
{
    if ((wrapLimit <= 0) || (str.Length <= wrapLimit))
    {
        return str;
    }

    var strSpan = str.AsSpan();
    int lineCount = ((strSpan.Length + (wrapLimit - 1)) / wrapLimit);
    StringBuilder stringBulder = new(str.Length + (lineCount * Environment.NewLine.Length));

    for (int i = 0; i < lineCount; ++i)
    {
        int substringStart = i * wrapLimit;
        int substringEnd = (i + 1) * wrapLimit;
        if (substringEnd > strSpan.Length)
        {
            substringEnd = strSpan.Length;
        }
        int substringLength = substringEnd - substringStart;
        stringBulder.Append(strSpan.Slice(substringStart, substringLength));
        if (i < lineCount - 1)
        {
            stringBulder.Append(Environment.NewLine);
        }
    }

    return stringBulder.ToString();
}

string ConvertStringSheetToSteamMarkdown(StringSheet stringSheet, int headerRow, int headerColumn, int wrapLimit)
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
            using (outputTableBuilder.NewTag("tr", true))
            {
                ++tableRow;
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    int tableColumn = columnIndex + 1;
                    bool isHeader = ((tableRow == headerRow) || (tableColumn == headerColumn));

                    string value = row[columnIndex] ?? string.Empty;


                    using (outputTableBuilder.NewTag(isHeader ? "th" : "td", false)) { outputTableBuilder.Append(WrapString(value, wrapLimit)); }
                }
            }
        }
    }

    return outputContents.ToString();
}
