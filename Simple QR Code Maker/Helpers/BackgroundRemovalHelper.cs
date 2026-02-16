using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;

namespace Simple_QR_Code_Maker.Helpers;

public static class BackgroundRemovalHelper
{
    /// <summary>
    /// Checks whether the on-device AI background removal feature is available.
    /// </summary>
    public static async Task<bool> CheckIsAvailableAsync()
    {
        try
        {
            AIFeatureReadyState state = ImageObjectExtractor.GetReadyState();
            if (state == AIFeatureReadyState.Ready)
                return true;

            if (state == AIFeatureReadyState.NotSupportedOnCurrentSystem
                || state == AIFeatureReadyState.DisabledByUser)
                return false;

            // NotReady — try to make it available
            await ImageObjectExtractor.EnsureReadyAsync();
            return ImageObjectExtractor.GetReadyState() == AIFeatureReadyState.Ready;
        }
        catch
        {
            return false;
        }
    }
}
