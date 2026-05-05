using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class FolderSummaryViewModel : ObservableRecipient, INavigationAware, ITitleBarBackNavigation
{
    private NavigationRestoreState? _backNavigationState;

    [ObservableProperty]
    public partial ObservableCollection<FolderSummaryItem> SummaryItems { get; set; } = [];

    [ObservableProperty]
    public partial string FolderName { get; set; } = string.Empty;

    public bool CanUseTitleBarBack => true;

    private INavigationService NavigationService { get; }

    public FolderSummaryViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public void OnNavigatedTo(object parameter)
    {
        _backNavigationState = null;

        if (parameter is FolderSummaryNavigationParameter navParam)
        {
            FolderName = navParam.FolderName;
            SummaryItems = new ObservableCollection<FolderSummaryItem>(navParam.Items);
            _backNavigationState = navParam.BackNavigationState;
        }
    }

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private void GoBack()
    {
        if (_backNavigationState is not null)
        {
            NavigationService.NavigateTo(_backNavigationState.PageKey, _backNavigationState.Parameter);
            return;
        }

        NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
    }

    public void NavigateBack() => GoBack();

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

    [RelayCommand]
    private async Task OpenFile(FolderSummaryItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(item.FilePath);
            _ = await Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open summary file: {ex.Message}");
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
