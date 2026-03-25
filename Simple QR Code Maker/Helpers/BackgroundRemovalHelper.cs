using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.Helpers;

public static class BackgroundRemovalHelper
{
    private static bool? _cachedAvailability;

    /// <summary>
    /// Checks whether the on-device AI background removal feature is available.
    /// The result is cached for the lifetime of the application to avoid repeated
    /// expensive WinRT calls that throw when the AI workload is not installed.
    /// </summary>
    public static async Task<bool> CheckIsAvailableAsync()
    {
        if (_cachedAvailability.HasValue)
            return _cachedAvailability.Value;

        // The AI imaging capability check requires package identity.
        // When running unpackaged (e.g. from bin output), the native
        // Microsoft.Windows.AI.Imaging.dll capability check fails with
        // "Access is denied" and can hang the UI thread.
        if (!RuntimeHelper.IsMSIX)
        {
            _cachedAvailability = false;
            return false;
        }

        return await CheckIsAvailableInternalAsync();
    }

    private static async Task<bool> CheckIsAvailableInternalAsync()
    {
        try
        {
            AIFeatureReadyState state = ImageObjectExtractor.GetReadyState();
            if (state == AIFeatureReadyState.Ready)
            {
                _cachedAvailability = true;
                return true;
            }

            if (state == AIFeatureReadyState.NotSupportedOnCurrentSystem
                || state == AIFeatureReadyState.DisabledByUser)
            {
                _cachedAvailability = false;
                return false;
            }

            // NotReady — try to make it available
            await ImageObjectExtractor.EnsureReadyAsync();
            bool ready = ImageObjectExtractor.GetReadyState() == AIFeatureReadyState.Ready;
            _cachedAvailability = ready;
            return ready;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Background removal availability check failed: {ex.Message}");
            _cachedAvailability = false;
            return false;
        }
    }
}
