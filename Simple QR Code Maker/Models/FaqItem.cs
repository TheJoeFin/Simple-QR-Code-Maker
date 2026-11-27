using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Models;

public enum FaqActionKind
{
    None = 0,
    OpenReadCodes = 1,
    OpenAdvancedTools = 2,
    OpenSettings = 3,
    OpenUrlBuilder = 4,
    OpenVCardBuilder = 5,
    OpenWifiBuilder = 6,
}

public class FaqItem
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public FaqActionKind ActionKind { get; set; } = FaqActionKind.None;
    public string? ActionText => ActionKind == FaqActionKind.None ? null : "Faq_ShowMe".GetLocalized();
    public FaqTopic Topics { get; init; }

    private static FaqItem Create(string key, FaqTopic topics = FaqTopic.None, FaqActionKind action = FaqActionKind.None)
        => new()
        {
            Title = $"{key}_Question".GetLocalized(),
            Content = $"{key}_Answer".GetLocalized(),
            Topics = topics,
            ActionKind = action,
        };

    public static readonly FaqItem[] AllFaqs = [
        Create("Faq_WhatIsAQRCode"),
        Create("Faq_WhatDoesQRStandFor"),
        Create("Faq_WhyCanQRCodesBeDangerous"),
        Create("Faq_AreTheseDynamicQRCodes"),
        Create("Faq_HowCanYouTellIfAQRCodeIsDynamic"),
        Create("Faq_WhatIsATwoDimensionalBarcode"),
        Create("Faq_HowToReadAQRCode", action: FaqActionKind.OpenReadCodes),
        Create("Faq_WhatIsTheURLBuilder", action: FaqActionKind.OpenUrlBuilder),
        Create("Faq_WhatIsAVCardQRCode", action: FaqActionKind.OpenVCardBuilder),
        Create("Faq_WhatIsAWiFiQRCode"),
        Create("Faq_HowDoIMakeAWiFiQRCode", action: FaqActionKind.OpenWifiBuilder),
        Create("Faq_WhatIsTheErrorCorrectionLevel", topics: FaqTopic.Size | FaqTopic.ErrorCorrection),
        Create("Faq_HowDoesAddingAnImageAffectTheQRCode", topics: FaqTopic.ErrorCorrection),
        Create("Faq_HowIsTheCenterImageStoredInSVGFiles"),
        Create("Faq_HowToPutAnImageInsideOfTheCenterOfAQRCode", topics: FaqTopic.Size | FaqTopic.ErrorCorrection | FaqTopic.Brands),
        Create("Faq_HowIsMinimumSizeCalculated", topics: FaqTopic.Size),
        Create("Faq_HowCanIChangeTheMaximumScanDistance", topics: FaqTopic.Size, action: FaqActionKind.OpenSettings),
        Create("Faq_WhatIsTheContrastRatio"),
        Create("Faq_AtTheMinimumSizeHowFarAwayCanAQRCodeBeScanned", topics: FaqTopic.Size),
        Create("Faq_WhatIfIPrintTheQRCodeSmallerThanTheMinimumSize", topics: FaqTopic.Size),
        Create("Faq_WhatTypeOfScannerOrPhoneCameraIsExpectedWhenPrintingAtTheMinimumSize", topics: FaqTopic.Size),
        Create("Faq_WillTheScanningAlwaysWorkIfMinimumSizeIsFollowed", topics: FaqTopic.Size),
        Create("Faq_WhyDoWeHaveToUseQRCodes"),
        Create("Faq_AreQRCodesEvilAndOrDumb"),
        Create("Faq_WhyMightQRCodeDetectionFail", topics: FaqTopic.Detection),
        Create("Faq_HowCanIImproveQRCodeDetectionInThisApp", topics: FaqTopic.Detection, action: FaqActionKind.OpenAdvancedTools),
        Create("Faq_WhatIsABrand", topics: FaqTopic.ErrorCorrection | FaqTopic.Brands),
        Create("Faq_HowDoICreateABrand", topics: FaqTopic.ErrorCorrection | FaqTopic.Brands),
        Create("Faq_HowDoIApplyABrand", topics: FaqTopic.Brands),
        Create("Faq_WhatDoesTheDefaultBrandSettingDo", topics: FaqTopic.Brands),
        Create("Faq_CanABrandStoreACenterLogoImage", topics: FaqTopic.Size | FaqTopic.Brands),
        ];
}
