namespace Connect4.App.Models;

/// <summary>The text of the About box, shared by the desktop and web versions.</summary>
public static class AboutInfo
{
    public const string Title = "About Connect 4";

    public const string Version = "Connect 4 Version 1.0";

    public const string PictureCaption = "Claus Pedersen – It-architect with a passion for AI and computer games. Connect 4 was written using Opus 5.5 in 2026";

    public static IReadOnlyList<string> Paragraphs { get; } =
    [
        "Connect 4 is a game for two players on a board with 7 columns and 6 rows. You play against the computer " +
        "or against a friend, and you can see the computer think in the analysis panel.",
        "Its brain searches ahead with alpha-beta search, a hash table and iterative deepening, and judges positions " +
        "by their threats and open lines. Near the end of the game it works out the exact result and plays perfectly.",
        "The search core is a C# port of Pascal Pons' C++ Connect 4 solver. The same brain runs as a Windows program " +
        "(WPF) and in the browser (Blazor WebAssembly).",
    ];
}
