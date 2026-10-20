using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Core.Storage;
using Simple_QR_Code_Maker.Core.Storage.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Maui.ViewModels;

public sealed partial class DesignsViewModel : ObservableObject
{
    private readonly QrDesignRepository designRepository;
    private readonly CreatorViewModel creatorViewModel;
    private readonly List<SavedDesignLibraryItem> designs = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string designName = string.Empty;

    [ObservableProperty]
    private SavedDesignLibraryItem? selectedDesign;

    [ObservableProperty]
    private string status = "Loading saved designs…";

    public ObservableCollection<SavedDesignLibraryItem> FilteredDesigns { get; } = [];

    public DesignsViewModel(QrDesignRepository designRepository, CreatorViewModel creatorViewModel)
    {
        this.designRepository = designRepository;
        this.creatorViewModel = creatorViewModel;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            QrDesignDocument document = await designRepository.LoadAsync();
            designs.Clear();
            designs.AddRange(document.Designs
                .OrderByDescending(design => design.IsDefault)
                .ThenBy(design => design.Name, StringComparer.OrdinalIgnoreCase)
                .Select(design => new SavedDesignLibraryItem(design)));
            ApplyFilter();
            Status = designs.Count == 0
                ? "No designs have been saved."
                : $"{designs.Count} saved design{(designs.Count == 1 ? string.Empty : "s")}.";
        }
        catch (Exception exception)
        {
            Status = $"Could not load saved designs: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        try
        {
            QrSavedDesign saved = await designRepository.SaveDesignAsync(new QrSavedDesign
            {
                Name = DesignName,
                Design = creatorViewModel.GetCurrentDesign(),
            });
            DesignName = string.Empty;
            await RefreshAsync();
            SelectedDesign = FilteredDesigns.FirstOrDefault(item => item.Design.Id == saved.Id);
            Status = $"Saved design “{saved.Name}”.";
        }
        catch (Exception exception)
        {
            Status = $"Could not save design: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedDesign is null)
        {
            Status = "Select a saved design to update.";
            return;
        }

        try
        {
            QrSavedDesign existing = SelectedDesign.Design;
            QrSavedDesign saved = await designRepository.SaveDesignAsync(existing with
            {
                Name = DesignName,
                Design = creatorViewModel.GetCurrentDesign(),
            });
            await RefreshAsync();
            SelectedDesign = FilteredDesigns.FirstOrDefault(item => item.Design.Id == saved.Id);
            Status = $"Updated design “{saved.Name}”.";
        }
        catch (Exception exception)
        {
            Status = $"Could not update design: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyAsync(SavedDesignLibraryItem? item)
    {
        if (item is null)
            return;

        creatorViewModel.ApplyDesign(item.Design.Design);
        await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
        Status = $"Applied design “{item.Design.Name}”.";
    }

    [RelayCommand]
    private async Task SetDefaultAsync(SavedDesignLibraryItem? item)
    {
        if (item is null)
            return;

        try
        {
            await designRepository.SetDefaultAsync(item.Design.Id);
            await RefreshAsync();
            SelectedDesign = FilteredDesigns.FirstOrDefault(design => design.Design.Id == item.Design.Id);
            Status = $"“{item.Design.Name}” is now the default design.";
        }
        catch (Exception exception)
        {
            Status = $"Could not set default design: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(SavedDesignLibraryItem? item)
    {
        if (item is null)
            return;

        try
        {
            await designRepository.DeleteDesignAsync(item.Design.Id);
            if (SelectedDesign?.Design.Id == item.Design.Id)
                SelectedDesign = null;

            await RefreshAsync();
            Status = $"Deleted design “{item.Design.Name}”.";
        }
        catch (Exception exception)
        {
            Status = $"Could not delete design: {exception.Message}";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDesignChanged(SavedDesignLibraryItem? value)
    {
        if (value is not null)
            DesignName = value.Design.Name;
    }

    private void ApplyFilter()
    {
        IEnumerable<SavedDesignLibraryItem> filtered = designs.Where(item =>
            string.IsNullOrWhiteSpace(SearchText)
            || item.Design.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
            || item.Design.Design.Content.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        FilteredDesigns.Clear();
        foreach (SavedDesignLibraryItem item in filtered)
            FilteredDesigns.Add(item);
    }
}

public sealed record SavedDesignLibraryItem(QrSavedDesign Design)
{
    public string DefaultLabel => Design.IsDefault ? "Default" : string.Empty;

    public string Content => Design.Design.Content;

    public string Summary => $"{Design.Design.PixelSize}px · {Design.Design.ErrorCorrectionLevel} correction";
}
