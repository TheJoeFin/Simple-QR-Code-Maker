namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
