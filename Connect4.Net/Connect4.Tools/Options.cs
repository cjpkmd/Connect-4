using System.Globalization;

namespace Connect4.Tools;

/// <summary>Command line options of the form "--name value".</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string> _values;

    private Options(Dictionary<string, string> values) => _values = values;

    /// <exception cref="ArgumentException">An argument is not "--name value", or a name is repeated.</exception>
    public static Options Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>();
        for (int i = 0; i < args.Count; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Count)
            {
                throw new ArgumentException($"Expected \"--name value\", found \"{args[i]}\".");
            }

            if (!values.TryAdd(args[i][2..], args[i + 1]))
            {
                throw new ArgumentException($"{args[i]} is given twice.");
            }
        }

        return new Options(values);
    }

    /// <exception cref="ArgumentException">An option is not one of <paramref name="names"/>.</exception>
    public void AllowOnly(params string[] names)
    {
        foreach (string name in _values.Keys.Where(name => !names.Contains(name)))
        {
            throw new ArgumentException($"Unknown option --{name}.");
        }
    }

    public string GetString(string name, string defaultValue) => _values.GetValueOrDefault(name, defaultValue);

    /// <exception cref="ArgumentException">The value is not a whole number in the range.</exception>
    public int GetInt(string name, int defaultValue, int min, int max)
    {
        if (!_values.TryGetValue(name, out string? text))
        {
            return defaultValue;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
        {
            throw new ArgumentException($"--{name} must be a whole number from {min} to {max}.");
        }

        return value;
    }
}
