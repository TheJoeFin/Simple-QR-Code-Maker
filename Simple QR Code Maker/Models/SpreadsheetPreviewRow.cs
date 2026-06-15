using System.Collections.ObjectModel;
#if WINDOWS
using WinRT;
#endif

namespace Simple_QR_Code_Maker.Models;

#if WINDOWS
[GeneratedBindableCustomProperty]
#endif
public sealed partial class SpreadsheetPreviewRow
{
    public required ObservableCollection<string> Cells { get; init; }
}
