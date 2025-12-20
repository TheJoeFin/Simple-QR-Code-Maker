using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class DecodingPage : Page
{
    public DecodingViewModel ViewModel
    {
        get;
    }

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();

        AdvancedToolsPanel.ViewModel.ImageProcessed += AdvancedToolsViewModel_ImageProcessed;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DecodingImageItems.CollectionChanged += DecodingImageItems_CollectionChanged;
    }

    private void DecodingImageItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && ViewModel.DecodingImageItems.Count > 0)
        {
            ViewModel.SelectedDecodingImageItem = ViewModel.DecodingImageItems[0];

            if (ViewModel.SelectedDecodingImageItem?.OriginalMagickImage != null)
            {
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.SelectedDecodingImageItem.OriginalMagickImage);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.DecodingImageItems) && ViewModel.DecodingImageItems.Count > 0)
        {
            ViewModel.SelectedDecodingImageItem = ViewModel.DecodingImageItems[0];

            if (ViewModel.SelectedDecodingImageItem?.OriginalMagickImage != null)
            {
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.SelectedDecodingImageItem.OriginalMagickImage);
            }
        }
    }

    private async void AdvancedToolsViewModel_ImageProcessed(object? sender, ImageMagick.MagickImage e)
    {
        if (ViewModel.SelectedDecodingImageItem != null)
        {
            ViewModel.SelectedDecodingImageItem.ProcessedMagickImage = e;
            await ViewModel.ApplyAdvancedToolsAndRedecodeCommand.ExecuteAsync(ViewModel.SelectedDecodingImageItem);
        }
    }

    private void GridViewContainer_DragOver(object sender, DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine($"contains Bitmap");
            e.AcceptedOperation = DataPackageOperation.Copy;
            return;
        }
        else if (dataView.Contains(StandardDataFormats.Uri))
        {
            Debug.WriteLine($"contains Uri");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine($"contains StorageItems");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void GridViewContainer_Drop(object sender, DragEventArgs e)
    {
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;
        def.Complete();

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine("Dropped StorageItem");
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            ViewModel.OpenAndDecodeStorageFiles(storageItems);
        }
    }
}
