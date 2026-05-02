using Microsoft.UI.Xaml;

using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly INavigationService _navigationService;

    public DefaultActivationHandler(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame?.Content == null;
    }

    protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        LaunchMode launchMode = await _localSettingsService.ReadSettingAsync<LaunchMode?>(nameof(LaunchMode))
            ?? LaunchMode.CreatingQrCodes;
        string initialPageKey = launchMode == LaunchMode.ReadingQrCodes
            ? typeof(DecodingViewModel).FullName!
            : typeof(MainViewModel).FullName!;

        _navigationService.NavigateTo(initialPageKey, args.Arguments);

        await Task.CompletedTask;
    }
}
