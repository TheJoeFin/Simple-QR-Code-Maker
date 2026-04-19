using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Helpers;

public static class ColorPickerListBuilder
{
    public static IReadOnlyList<ColorPickerListItem> FromBrands(IEnumerable<BrandItem> brands)
    {
        List<ColorPickerListItem> items = [];

        foreach (BrandItem brand in brands)
        {
            if (brand.Foreground is Windows.UI.Color foreground)
                items.Add(new ColorPickerListItem(foreground, BuildLabel("Foreground", brand.Name)));

            if (brand.Background is Windows.UI.Color background)
                items.Add(new ColorPickerListItem(background, BuildLabel("Background", brand.Name)));
        }

        return items;
    }

    public static IReadOnlyList<ColorPickerListItem> FromHistory(IEnumerable<HistoryItem> historyItems)
    {
        List<ColorPickerListItem> items = [];

        foreach (HistoryItem historyItem in historyItems)
        {
            items.Add(new ColorPickerListItem(historyItem.Foreground, BuildLabel("Foreground", historyItem.CodesContent)));
            items.Add(new ColorPickerListItem(historyItem.Background, BuildLabel("Background", historyItem.CodesContent)));
        }

        return items;
    }

    public static void ReplaceItems(ObservableCollection<ColorPickerListItem> target, IEnumerable<ColorPickerListItem> source)
    {
        target.Clear();

        foreach (ColorPickerListItem item in source)
        {
            target.Add(item);
        }
    }

    private static string BuildLabel(string prefix, string source)
    {
        return $"{prefix}, {NormalizeSource(source)}";
    }

    private static string NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "(empty)";

        string normalized = source
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length == 0 ? "(empty)" : normalized;
    }
}
