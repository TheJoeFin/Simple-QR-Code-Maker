using System.Collections.ObjectModel;
using WinRT;

namespace Simple_QR_Code_Maker.Models;

[GeneratedBindableCustomProperty]
public sealed partial class SpreadsheetPreviewRow
{
    public required ObservableCollection<string> Cells { get; init; }
}
