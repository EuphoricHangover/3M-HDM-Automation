namespace HdmAutomation.Cli;

internal sealed class Options
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Options Parse(IEnumerable<string> args, ISet<string> flags, ISet<string> valued)
    {
        var options = new Options();
        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var arg = queue.Dequeue();
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{arg}'.");

            var name = arg[2..];
            if (flags.Contains(name)) options._values[name] = null;
            else if (valued.Contains(name))
            {
                if (queue.Count == 0) throw new ArgumentException($"Option --{name} needs a value.");
                options._values[name] = queue.Dequeue();
            }
            else throw new ArgumentException($"Unknown option --{name}.");
        }
        return options;
    }

    public bool Has(string name) => _values.ContainsKey(name);
    public string Get(string name, string fallback) => _values.TryGetValue(name, out var v) && v is not null ? v : fallback;
    public string? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;
    public int GetInt(string name, int fallback) => _values.TryGetValue(name, out var v) && v is not null ? int.Parse(v) : fallback;
}
