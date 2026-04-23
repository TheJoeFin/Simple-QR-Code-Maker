using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

/// <summary>
/// Minimal delimited-text parser. Handles quoted fields, embedded delimiters,
/// and doubled-quote escaping (""). Does not depend on any external packages.
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// Parses delimited text into a list of rows, each row being a list of field strings.
    /// </summary>
    public static List<List<string>> Parse(string csv, char delimiter = ',')
    {
        List<List<string>> rows = [];
        if (string.IsNullOrEmpty(csv))
            return rows;

        // Normalise line endings to LF only so we can treat \n as the row separator.
        csv = csv.Replace("\r\n", "\n").Replace('\r', '\n');

        List<string> currentRow = [];
        StringBuilder currentField = new();
        bool inQuotes = false;
        int i = 0;

        while (i < csv.Length)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Peek ahead: "" is an escaped quote inside a quoted field.
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                    }
                    else
                    {
                        // Closing quote.
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == delimiter)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    i++;
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
        }

        // Flush the last field and row (file may not end with a newline).
        currentRow.Add(currentField.ToString());
        // Only add the last row if it is non-empty (avoids a phantom blank row for
        // files that do end with a newline).
        if (currentRow.Count > 1 || (currentRow.Count == 1 && currentRow[0].Length > 0))
            rows.Add(currentRow);

        return rows;
    }

    public static string Serialize(IEnumerable<IReadOnlyList<string>> rows, char delimiter = ',')
    {
        StringBuilder builder = new();
        bool isFirstRow = true;

        foreach (IReadOnlyList<string> row in rows)
        {
            if (!isFirstRow)
                builder.Append("\r\n");

            for (int index = 0; index < row.Count; index++)
            {
                if (index > 0)
                    builder.Append(delimiter);

                builder.Append(EscapeField(row[index], delimiter));
            }

            isFirstRow = false;
        }

        return builder.ToString();
    }

    private static string EscapeField(string field, char delimiter)
    {
        bool requiresQuotes = field.Contains(delimiter)
            || field.Contains('"')
            || field.Contains('\r')
            || field.Contains('\n');

        if (!requiresQuotes)
            return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
