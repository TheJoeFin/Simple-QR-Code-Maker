using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

/// <summary>
/// Minimal RFC-4180 CSV parser. Handles quoted fields, embedded commas, and
/// doubled-quote escaping (""). Does not depend on any external packages.
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// Parses CSV text into a list of rows, each row being a list of field strings.
    /// </summary>
    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(csv))
            return rows;

        // Normalise line endings to LF only so we can treat \n as the row separator.
        csv = csv.Replace("\r\n", "\n").Replace('\r', '\n');

        var currentRow = new List<string>();
        var currentField = new StringBuilder();
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
                else if (c == ',')
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
}
