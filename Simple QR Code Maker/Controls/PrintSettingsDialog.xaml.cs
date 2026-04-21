using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class PrintSettingsDialog : ContentDialog
{
    private static readonly int[] CodesPerPageOptions = [1, 2, 4, 6, 9, 12];

    public PrintSettingsDialog(PrintJobSettings initialSettings)
    {
        InitializeComponent();

        int index = Array.IndexOf(CodesPerPageOptions, initialSettings.CodesPerPage);
        CodesPerPageCombo.SelectedIndex = index >= 0 ? index : 2;
        MarginMmBox.Value = initialSettings.MarginMm;
        ShowLabelsSwitch.IsOn = initialSettings.ShowLabels;
    }

    public PrintJobSettings ResultSettings => new()
    {
        CodesPerPage = CodesPerPageCombo.SelectedIndex >= 0
            ? CodesPerPageOptions[CodesPerPageCombo.SelectedIndex]
            : 4,
        MarginMm = double.IsNaN(MarginMmBox.Value) ? 10 : MarginMmBox.Value,
        ShowLabels = ShowLabelsSwitch.IsOn,
    };
}
