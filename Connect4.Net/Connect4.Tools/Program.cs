using Connect4.Tools;

if (args is [var group, var command, .. var rest])
{
    try
    {
        var options = Options.Parse(rest);
        switch (group, command)
        {
            case ("book", "generate"):
                return BookCommands.Generate(options);
            case ("book", "verify"):
                return BookCommands.Verify(options);
            case ("endgame", "measure"):
                return EndgameCommands.Measure(options);
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
      Connect4.Tools book generate [--depth 9] [--workers N] [--table 24] [--out OpeningBook.txt]
          Solves every position after --depth plies and writes the opening book. A stopped run
          (Ctrl+C) continues from <out>.partial when the same command is run again.
      Connect4.Tools book verify [--book OpeningBook.txt] [--sample 200] [--min-ply 3] [--seed S] [--workers N] [--table 24]
          Checks the first-move scores and the back-up, and solves a random sample of the book again.
      Connect4.Tools endgame measure [--from 24] [--to 33] [--games 100] [--per-set 50] [--limit-ms 1700] [--cap 30] [--seed 1] [--workers 1] [--table 24] [--tests <folder>]
          Times the engine's endgame solve (every move, empty table) by number of empty cells, on positions
          from engine games and from the test sets, and suggests the endgame threshold for the time limit.
    """);
return 1;
