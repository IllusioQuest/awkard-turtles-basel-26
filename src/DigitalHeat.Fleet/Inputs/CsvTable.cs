using System.Globalization;

namespace DigitalHeat.Fleet;

/// <summary>
/// Minimal CSV reader for the repo's data files: header row, RFC 4180 quoting, invariant culture.
/// Lines starting with '#' (before the header) are comments.
/// </summary>
public sealed class CsvTable
{
    private readonly Dictionary<string, int> _columns;
    public IReadOnlyList<string[]> Rows { get; }
    public string Path { get; }

    private CsvTable(string path, string[] header, List<string[]> rows)
    {
        Path = path;
        Rows = rows;
        _columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < header.Length; i++) _columns[header[i].Trim()] = i;
    }

    public static CsvTable Read(string path)
    {
        string[]? header = null;
        var rows = new List<string[]>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (header is null && line.StartsWith('#')) continue;
            var fields = SplitLine(line);
            if (header is null) header = fields;
            else rows.Add(fields);
        }
        if (header is null) throw new InvalidDataException($"No header in {path}.");
        return new CsvTable(path, header, rows);
    }

    public bool Has(string column) => _columns.ContainsKey(column);

    public int Col(string column) => _columns.TryGetValue(column, out int i)
        ? i : throw new InvalidDataException($"Column '{column}' missing in {Path}.");

    public string Str(string[] row, string column) => row[Col(column)];

    public double Num(string[] row, string column) =>
        double.Parse(row[Col(column)], NumberStyles.Float, CultureInfo.InvariantCulture);

    public static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
