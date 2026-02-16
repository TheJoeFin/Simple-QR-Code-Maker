using Microsoft.Windows.AppLifecycle;

using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.ViewModels;

using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Activation;

public class ShareTargetActivationHandler : ActivationHandler<ShareTargetActivatedEventArgs>
{
    private readonly INavigationService _navigationService;

    public ShareTargetActivationHandler(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    protected override async Task HandleInternalAsync(ShareTargetActivatedEventArgs args)
    {
        ShareOperation shareOperation = args.ShareOperation;

        try
        {
            if (shareOperation.Data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await shareOperation.Data.GetStorageItemsAsync();
                StorageFile? imageFile = items.OfType<StorageFile>().FirstOrDefault();

                if (imageFile is not null)
                {
                    _navigationService.NavigateTo(typeof(DecodingViewModel).FullName!, imageFile);
                    shareOperation.ReportCompleted();
                    return;
                }
            }

            if (shareOperation.Data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
            {
                // For bitmap data, navigate to decoding page without a file parameter;
                // the user can paste from clipboard once there.
                _navigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
                shareOperation.ReportCompleted();
                return;
            }

            string? sharedText = null;

            if (shareOperation.Data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Uri))
            {
                Uri? uri = await shareOperation.Data.GetUriAsync();
                sharedText = uri?.AbsoluteUri;
            }
            else if (shareOperation.Data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                sharedText = await shareOperation.Data.GetTextAsync();
            }

            if (!string.IsNullOrWhiteSpace(sharedText))
            {
                _navigationService.NavigateTo(typeof(MainViewModel).FullName!, sharedText);
            }

            shareOperation.ReportCompleted();
        }
        catch
        {
            shareOperation.ReportCompleted();
        }
    }
}
