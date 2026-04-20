using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class UrlBuilderDialog : ContentDialog
{
    private readonly List<UrlBuilderLine> _lines;

    private int _currentLineIndex;
    private bool _isLoadingLine;
    private bool _currentUsesAuthorityDelimiter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewUrl))]
    public partial string Scheme { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewUrl))]
    public partial string Authority { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewUrl))]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewUrl))]
    public partial string Fragment { get; set; } = string.Empty;

    public ObservableCollection<UrlBuilderQueryParameter> QueryParameters { get; } = [];

    public bool HasMultipleLines => _lines.Count > 1;

    public bool CanGoPrevious => _currentLineIndex > 0;

    public bool CanGoNext => _currentLineIndex < _lines.Count - 1;

    public string CurrentLineLabel => $"Line {_currentLineIndex + 1} of {_lines.Count}";

    public Visibility LineNavigationVisibility => HasMultipleLines ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyQueryParametersVisibility => QueryParameters.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string PreviewUrl => UrlBuilderHelper.SerializeLine(CreateCurrentLineState());

    public string? ResultText { get; private set; }

    public UrlBuilderDialog(string? initialText)
    {
        _lines = [.. UrlBuilderHelper.ParseText(initialText).Select(line => line.Clone())];

        InitializeComponent();

        QueryParameters.CollectionChanged += QueryParameters_CollectionChanged;
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;

        LoadLine(0);
    }

    [RelayCommand]
    private void AddQueryParameter()
    {
        QueryParameters.Add(new UrlBuilderQueryParameter());
    }

    [RelayCommand]
    private void GoToPreviousLine()
    {
        if (!CanGoPrevious)
            return;

        SaveCurrentLine();
        LoadLine(_currentLineIndex - 1);
    }

    [RelayCommand]
    private void GoToNextLine()
    {
        if (!CanGoNext)
            return;

        SaveCurrentLine();
        LoadLine(_currentLineIndex + 1);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        AuthorityTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SaveCurrentLine();
        ResultText = UrlBuilderHelper.SerializeText(_lines);
    }

    private void RemoveQueryParameterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is UrlBuilderQueryParameter parameter)
            QueryParameters.Remove(parameter);
    }

    private void LoadLine(int index)
    {
        _isLoadingLine = true;
        _currentLineIndex = index;

        UrlBuilderLine line = _lines[index];
        _currentUsesAuthorityDelimiter = line.UsesAuthorityDelimiter;
        Scheme = line.Scheme;
        Authority = line.Authority;
        Path = line.Path;
        Fragment = line.Fragment;

        ReplaceQueryParameters(line.QueryParameters);

        _isLoadingLine = false;
        NotifyNavigationStateChanged();
        NotifyPreviewStateChanged();
    }

    private void SaveCurrentLine()
    {
        if (_currentLineIndex < 0 || _currentLineIndex >= _lines.Count)
            return;

        _lines[_currentLineIndex] = CreateCurrentLineState();
    }

    private UrlBuilderLine CreateCurrentLineState()
    {
        bool usesAuthorityDelimiter = Scheme.Trim().Length > 0 || _currentUsesAuthorityDelimiter;

        return new UrlBuilderLine
        {
            Scheme = Scheme,
            Authority = Authority,
            Path = Path,
            Fragment = Fragment,
            UsesAuthorityDelimiter = usesAuthorityDelimiter,
            QueryParameters = [.. QueryParameters.Select(parameter => parameter.Clone())]
        };
    }

    private void ReplaceQueryParameters(IEnumerable<UrlBuilderQueryParameter> parameters)
    {
        foreach (UrlBuilderQueryParameter parameter in QueryParameters)
            parameter.PropertyChanged -= QueryParameter_PropertyChanged;

        QueryParameters.Clear();

        foreach (UrlBuilderQueryParameter parameter in parameters)
        {
            UrlBuilderQueryParameter clone = parameter.Clone();
            QueryParameters.Add(clone);
        }
    }

    private void QueryParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (UrlBuilderQueryParameter parameter in e.OldItems)
                parameter.PropertyChanged -= QueryParameter_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (UrlBuilderQueryParameter parameter in e.NewItems)
                parameter.PropertyChanged += QueryParameter_PropertyChanged;
        }

        NotifyPreviewStateChanged();
    }

    private void QueryParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyPreviewStateChanged();
    }

    partial void OnSchemeChanged(string value) => NotifyPreviewStateChanged();

    partial void OnAuthorityChanged(string value) => NotifyPreviewStateChanged();

    partial void OnPathChanged(string value) => NotifyPreviewStateChanged();

    partial void OnFragmentChanged(string value) => NotifyPreviewStateChanged();

    private void NotifyNavigationStateChanged()
    {
        OnPropertyChanged(nameof(HasMultipleLines));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CurrentLineLabel));
        OnPropertyChanged(nameof(LineNavigationVisibility));
    }

    private void NotifyPreviewStateChanged()
    {
        if (_isLoadingLine)
            return;

        OnPropertyChanged(nameof(PreviewUrl));
        OnPropertyChanged(nameof(EmptyQueryParametersVisibility));
    }
}
