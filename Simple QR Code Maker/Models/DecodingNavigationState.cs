using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Simple_QR_Code_Maker.Models;

public class DecodingNavigationState
{
    public HistoryItem? NavigationHistoryItem { get; init; }

    public DecodingSourceKind CurrentSourceKind { get; init; } = DecodingSourceKind.File;

    public string? CurrentSourceFilePath { get; init; }

    public string? CurrentCachedImagePath { get; init; }

    public string? CurrentFolderPath { get; init; }

    public string? CurrentFolderFilePath { get; init; }

    public string InfoBarMessage { get; init; } = string.Empty;

    public bool IsInfoBarShowing { get; init; }

    public string DecodedContentInfoBarTitle { get; init; } = "QR Code Content";

    public InfoBarSeverity DecodedContentInfoBarSeverity { get; init; } = InfoBarSeverity.Informational;

    public bool IsDecodedContentUrl { get; init; }

    public bool IsLikelyRedirector { get; init; }

    public string RedirectorWarningMessage { get; init; } = string.Empty;

    public bool IsRedirectorWarningVisible { get; init; }

    public bool IsAdvancedToolsVisible { get; init; }

    public bool IsCameraPaneOpen { get; init; }

    public bool IsFaqPaneOpen { get; init; }

    public bool IsDecodingHistoryPaneOpen { get; init; }

    public bool IsFolderPaneOpen { get; init; }

    public bool IsCutOutImagesPaneOpen { get; init; }

    public string FolderPaneFolderName { get; init; } = string.Empty;

    public IReadOnlyList<DecodingImageNavigationState> DecodingImageItems { get; init; } = [];

    public IReadOnlyList<FolderFileNavigationState> FolderFiles { get; init; } = [];

    public DecodingImageNavigationState? CurrentDecodingItem { get; init; }
}

public class FolderFileNavigationState
{
    public string FilePath { get; init; } = string.Empty;

    public IReadOnlyList<DecodingImageNavigationState> CutOuts { get; init; } = [];
}

public class DecodingImageNavigationState
{
    public string ImagePath { get; init; } = string.Empty;

    public string CachedBitmapPath { get; init; } = string.Empty;

    public int ImagePixelWidth { get; init; }

    public int ImagePixelHeight { get; init; }

    public bool IsNoCodesWarningDismissed { get; init; }

    public string Label { get; init; } = string.Empty;

    public string ParentFileName { get; init; } = string.Empty;

    public IReadOnlyList<TextBorderNavigationState> CodeBorders { get; init; } = [];
}

public class TextBorderNavigationState
{
    public string Text { get; init; } = string.Empty;

    public Rect BorderRect { get; init; } = Rect.Empty;
}
