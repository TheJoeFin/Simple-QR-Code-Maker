namespace Simple_QR_Code_Maker.Contracts.ViewModels;

public interface ITitleBarBackNavigation
{
    bool CanUseTitleBarBack { get; }

    void NavigateBack();
}
