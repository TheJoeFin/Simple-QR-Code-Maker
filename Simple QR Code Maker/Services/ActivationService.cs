using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Simple_QR_Code_Maker.Activation;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Views;

namespace Simple_QR_Code_Maker.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler, IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        await _themeSelectorService.InitializeAsync();

        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            if (_shell is FrameworkElement root)
                root.FlowDirection = Enum.Parse<FlowDirection>("App_FlowDirection".GetLocalized());
            App.MainWindow.Content = _shell ?? new Frame();
        }

        if (_shell is ShellPage shellPage)
            shellPage.ConfigureTitleBar();

        await _themeSelectorService.SetRequestedThemeAsync();
        await HandleActivationAsync(activationArgs);
        App.MainWindow.Activate();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        IActivationHandler? activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

}
