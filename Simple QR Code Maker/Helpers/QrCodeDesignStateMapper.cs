using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class QrCodeDesignStateMapper
{
    public static QrCodeDesignState FromHistoryItem(HistoryItem historyItem)
    {
        return new QrCodeDesignState
        {
            CodesContent = historyItem.CodesContent,
            ContentKind = historyItem.ContentKind,
            MultiLineCodeModeOverride = historyItem.MultiLineCodeModeOverride,
            Foreground = historyItem.Foreground,
            Background = historyItem.Background,
            ErrorCorrection = historyItem.ErrorCorrection,
            LogoImagePath = historyItem.LogoImagePath,
            LogoEmoji = historyItem.LogoEmoji,
            LogoEmojiStyle = historyItem.LogoEmojiStyle,
            LogoSizePercentage = historyItem.LogoSizePercentage,
            LogoPaddingPixels = historyItem.LogoPaddingPixels,
            FramePreset = historyItem.FramePreset,
            FrameText = historyItem.FrameText,
        };
    }

    public static HistoryItem ToHistoryItem(QrCodeDesignState state)
    {
        return new HistoryItem
        {
            CodesContent = state.CodesContent,
            ContentKind = state.ContentKind,
            MultiLineCodeModeOverride = state.MultiLineCodeModeOverride,
            Foreground = state.Foreground,
            Background = state.Background,
            ErrorCorrection = state.ErrorCorrection,
            LogoImagePath = state.LogoImagePath,
            LogoEmoji = state.LogoEmoji,
            LogoEmojiStyle = state.LogoEmojiStyle,
            LogoSizePercentage = state.LogoSizePercentage,
            LogoPaddingPixels = state.LogoPaddingPixels,
            FramePreset = state.FramePreset,
            FrameText = state.FrameText,
        };
    }

    public static BrandItem ToBrandItem(QrCodeDesignState state, string name, BrandCreationOptions options)
    {
        return new BrandItem
        {
            Name = name.Trim(),
            Foreground = options.IncludeForeground ? state.Foreground : null,
            Background = options.IncludeBackground ? state.Background : null,
            UrlContent = options.IncludeUrl ? state.CodesContent : null,
            ErrorCorrectionLevelAsString = options.IncludeErrorCorrection ? state.ErrorCorrection.ToString() : null,
            LogoImagePath = options.IncludeCenterImage ? state.LogoImagePath : null,
            LogoEmoji = options.IncludeCenterImage ? state.LogoEmoji : null,
            LogoEmojiStyle = options.IncludeCenterImage ? state.LogoEmojiStyle : null,
            LogoSizePercentage = options.IncludeCenterImage ? state.LogoSizePercentage : null,
            LogoPaddingPixels = options.IncludeCenterImage ? state.LogoPaddingPixels : null,
            FramePreset = options.IncludeFrame ? state.FramePreset : null,
            FrameText = options.IncludeFrame && state.FramePreset != QrFramePreset.None
                ? state.FrameText?.Trim()
                : null,
        };
    }
}
