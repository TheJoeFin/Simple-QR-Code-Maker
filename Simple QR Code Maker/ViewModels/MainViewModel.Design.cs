using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Models;
using Windows.UI;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class MainViewModel
{
    private QrCodeDesignState? designBeforePreset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModuleStyle))]
    [NotifyPropertyChangedFor(nameof(EyeStyle))]
    [NotifyPropertyChangedFor(nameof(CaptionIcon))]
    [NotifyPropertyChangedFor(nameof(UseCustomFrameAccent))]
    [NotifyPropertyChangedFor(nameof(FrameAccentColor))]
    [NotifyPropertyChangedFor(nameof(UseFrameGradient))]
    [NotifyPropertyChangedFor(nameof(FrameGradientEndColor))]
    public partial QrCodeAppearance Appearance { get; set; } = new();

    public QrModuleStyle ModuleStyle
    {
        get => Appearance.ModuleStyle;
        set
        {
            if (value != QrModuleStyle.Square)
                QrPaddingModules = Math.Max(4, QrPaddingModules);
            Appearance = Appearance with { ModuleStyle = value };
        }
    }

    public QrEyeStyle EyeStyle
    {
        get => Appearance.EyeStyle;
        set
        {
            if (value != QrEyeStyle.Square)
                QrPaddingModules = Math.Max(4, QrPaddingModules);
            Appearance = Appearance with { EyeStyle = value };
        }
    }

    public QrCaptionIcon CaptionIcon
    {
        get => Appearance.CaptionIcon;
        set => Appearance = Appearance with { CaptionIcon = value };
    }

    public bool UseCustomFrameAccent
    {
        get => Appearance.FrameAccent.HasValue;
        set => Appearance = Appearance with
        {
            FrameAccent = value ? FrameAccentColor : null,
            FrameGradientEnd = value ? Appearance.FrameGradientEnd : null,
        };
    }

    public Color FrameAccentColor
    {
        get => Appearance.FrameAccent ?? ForegroundColor;
        set => Appearance = Appearance with { FrameAccent = value };
    }

    public bool UseFrameGradient
    {
        get => Appearance.FrameGradientEnd.HasValue;
        set => Appearance = Appearance with
        {
            FrameAccent = value ? FrameAccentColor : Appearance.FrameAccent,
            FrameGradientEnd = value ? FrameGradientEndColor : null,
        };
    }

    public Color FrameGradientEndColor
    {
        get => Appearance.FrameGradientEnd ?? Color.FromArgb(255, 244, 153, 192);
        set => Appearance = Appearance with { FrameGradientEnd = value, FrameAccent = FrameAccentColor };
    }

    partial void OnAppearanceChanged(QrCodeAppearance value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    [RelayCommand]
    private void ApplyDesignPreset(QrDesignPreset preset)
    {
        designBeforePreset = CreateCurrentDesignState();
        ApplyVisualDesign(preset.ApplyTo(designBeforePreset));
        UndoDesignPresetCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndoDesignPreset() => designBeforePreset is not null;

    [RelayCommand(CanExecute = nameof(CanUndoDesignPreset))]
    private void UndoDesignPreset()
    {
        if (designBeforePreset is not QrCodeDesignState previousDesign)
            return;

        designBeforePreset = null;
        ApplyVisualDesign(previousDesign);
        UndoDesignPresetCommand.NotifyCanExecuteChanged();
    }

    private void ApplyVisualDesign(QrCodeDesignState state)
    {
        ForegroundColor = state.Foreground;
        BackgroundColor = state.Background;
        FramePreset = state.FramePreset;
        FrameTextSource = state.FrameTextSource;
        FrameText = state.FrameText ?? string.Empty;
        Appearance = state.Appearance;
        if (state.QrPaddingModules.HasValue)
            QrPaddingModules = state.QrPaddingModules.Value;
    }
}
