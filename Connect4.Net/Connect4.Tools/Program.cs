using Connect4.Tools;

if (args is ["book", var command, .. var rest])
{
    try
    {
        var options = Options.Parse(rest);
        switch (command)
        {
            case "generate":
                return BookCommands.Generate(options);
            case "verify":
                return BookCommands.Verify(options);
        }
    }
    catch (Exception e) when (e is ArgumentException or FormatException or IOException)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

Console.Error.WriteLine("""
    Usage:
      Connect4.Tools book generate [--depth 6] [--workers N] [--table 24] [--out OpeningBook.txt]
          Solves every position after --depth plies and writes the opening book. A stopped run
          (Ctrl+C) continues from <out>.partial when the same command is run again.
      Connect4.Tools book verify [--book OpeningBook.txt] [--sample 200] [--min-ply 3] [--seed S] [--workers N] [--table 24]
          Checks the first-move scores and the back-up, and solves a random sample of the book again.
    """);
return 1;
