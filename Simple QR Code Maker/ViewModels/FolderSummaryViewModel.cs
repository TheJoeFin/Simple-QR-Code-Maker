using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Models;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class FolderSummaryViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    public partial List<FolderSummaryItem> SummaryItems { get; set; } = [];

    [ObservableProperty]
    public partial string FolderName { get; set; } = string.Empty;

    private INavigationService NavigationService { get; }

    public FolderSummaryViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is FolderSummaryNavigationParameter navParam)
        {
            FolderName = navParam.FolderName;
            SummaryItems = navParam.Items;
        }
    }

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
    }

    [RelayCommand]
    private async Task SaveAsCsv()
    {
        if (SummaryItems.Count == 0)
            return;

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = string.IsNullOrEmpty(FolderName) ? "folder-summary" : FolderName,
        };
        picker.FileTypeChoices.Add("CSV File", [".csv"]);

        Window saveWindow = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        StringBuilder sb = new();
        sb.AppendLine("File Name,QR Code Count,Contents");

        foreach (FolderSummaryItem item in SummaryItems)
            sb.AppendLine($"{CsvEscape(item.FileName)},{item.QrCodeCount},{CsvEscape(item.QrCodeContents)}");

        await FileIO.WriteTextAsync(file, sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
