
namespace Simple_QR_Code_Maker.Models;

public class FaqItem
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public static readonly FaqItem[] AllFaqs = [
        new FaqItem { Title = "What is a QR Code?", Content = "A QR Code is a two-dimensional barcode that is readable by smartphones. It allows to encode over 4000 characters in a two dimensional barcode. QR Codes may be used to display text to the user, to open a URL, and many other things. But mostly just for opening websites."},
        new FaqItem { Title = "Are these dynamic QR Codes?", Content = "QR Codes are not dynamic on their own. To make a QR Code dynamic you need to use a link which will redirect/forward to another link." },
        new FaqItem { Title = "How can you tell if a QR Code is dynamic?", Content = "If when you scan the QR Code and it goes to a website say, https://www.example.com but when you use this app to read the code and it is a totally different URL like http://JoeFinApps.com/123, then the QR Code is likely dynamic." },
        new FaqItem { Title = "How to read a QR Code?", Content = "To read a QR Code, you need a smartphone with a camera. Android, iOS, and Windows all support QR Code scanning in the native default camera apps. To scan the code, simply open the app and hold the camera in front of it. The app will automatically recognize the code and act accordingly. NOTE: this does not mean the webpage you are sent to is exactly what is encoded into the QR Code." },
        new FaqItem { Title = "Put an image inside of the center of a QR Code?", Content = "To create a QR Code, you need a QR Code generator. There are many free QR Code generators available online. To create a QR Code, simply enter the text you want to encode and click on the generate button. The QR Code will be generated instantly and you can download it as an image file." },
        new FaqItem { Title = "How is minimum size calculated?", Content = "The minimum size of a QR Code is calculated based experimental data. QR Codes at several sizes were printed using a 600dpi laser printer on white paper. The codes were scanned and a relationship between code size and scan distance was developed. The contrast between the QR Code's foreground and background is also factored into the minimum size." },
        new FaqItem { Title = "What is the contrast ratio?", Content = "The contrast ratio is the difference in luminance or color that makes an object distinguishable. In the context of QR Codes, the contrast ratio is the difference in color between the foreground and background of the code. A higher contrast ratio makes the code easier to scan and read. The minimum recommended contrast ratio for QR Codes is 2.5:1." },
        new FaqItem { Title = "At the minimum size, how far away can a QR Code be scanned?", Content = "36 inches or 1 meter is roughly the maximum scan distance of a QR Code printed at the minimum size." },
        new FaqItem { Title = "What type of scanner or phone camera is expected when printing at the minimum size?", Content = "The scanning experiments were carried out using an iPhone 13 and the default iOS code-scanning app and the main camera. The camera is a 12 Megapixel (MP) camera. Any modern Android or iOS phone after 2018 should be able to scan QR Codes." },
        new FaqItem { Title = "Will the scanning always work if minimum size is followed?", Content = "No. The minimum size assumes good conditions such as: a well lit environment, matte printed surface, a decently modern scanning device." },
        new FaqItem { Title = "Why?", Content = "Typing in URLs exactly is tedious... so having the camera scan it was easier I guess." },
        new FaqItem { Title = "Are QR Codes evil and/or dumb", Content = "A little of both, but they can be a handy way to get people to websites." },
        ];
}
