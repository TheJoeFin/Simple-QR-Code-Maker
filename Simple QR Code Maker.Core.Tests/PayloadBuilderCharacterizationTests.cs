using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Core.Models;
using Simple_QR_Code_Maker.Core.Services;
using Simple_QR_Code_Maker.Rendering;
using Xunit;

namespace Simple_QR_Code_Maker.Core.Tests;

public sealed class PayloadBuilderCharacterizationTests
{
    [Fact]
    public void UrlBuilder_SerializesEscapedComponentsAndMultipleLines()
    {
        UrlBuilderLine firstLine = new()
        {
            Scheme = "https",
            Authority = "example.com",
            Path = "/a path/東京",
            Fragment = "section one",
            QueryParameters =
            [
                new("name", "Ada Lovelace"),
                new("empty", string.Empty),
                new("emoji", "😀"),
            ],
        };
        UrlBuilderLine secondLine = new()
        {
            Path = "relative path",
            QueryParameters = [new("q", "a&b")],
        };

        string serialized = UrlBuilderHelper.SerializeText([firstLine, secondLine]);

        Assert.Equal(
            "https://example.com/a%20path/%E6%9D%B1%E4%BA%AC?name=Ada%20Lovelace&empty=&emoji=%F0%9F%98%80#section%20one\r\nrelative%20path?q=a%26b",
            serialized);
        Assert.Equal(serialized, UrlBuilderHelper.SerializeText(UrlBuilderHelper.ParseText(serialized)));
    }

    [Fact]
    public void WifiBuilder_EscapesReservedCharactersAndPreservesHiddenWepNetwork()
    {
        WifiBuilderState state = new()
        {
            Ssid = "Cafe;West:5G",
            AuthenticationType = WifiAuthenticationType.WEP,
            Password = "p\\a;ss,:\"word",
            IsHiddenNetwork = true,
        };

        string serialized = WifiBuilderHelper.Serialize(state);
        bool parsed = WifiBuilderHelper.TryParse(serialized, out WifiBuilderState parsedState);

        Assert.Equal("WIFI:S:Cafe\\;West\\:5G;T:WEP;P:p\\\\a\\;ss\\,\\:\\\"word;H:true;;", serialized);
        Assert.True(parsed);
        Assert.Equal(state.Ssid, parsedState.Ssid);
        Assert.Equal(state.Password, parsedState.Password);
        Assert.Equal(state.AuthenticationType, parsedState.AuthenticationType);
        Assert.True(parsedState.IsHiddenNetwork);
    }

    [Fact]
    public void EmailBuilder_SerializesOptionalRecipientsAndMultilineBody()
    {
        EmailBuilderState state = new()
        {
            To = "ada@example.com",
            Cc = "team@example.com",
            Bcc = "audit@example.com",
            Subject = "Review & approve",
            Body = "First line\r\nSecond line: 東京",
        };

        string serialized = EmailBuilderHelper.Serialize(state);
        bool parsed = EmailBuilderHelper.TryParse(serialized, out EmailBuilderState parsedState);

        Assert.Equal(
            "mailto:ada@example.com?cc=team%40example.com&bcc=audit%40example.com&subject=Review%20%26%20approve&body=First%20line%0D%0ASecond%20line%3A%20%E6%9D%B1%E4%BA%AC",
            serialized);
        Assert.True(parsed);
        Assert.Equal(state.To, parsedState.To);
        Assert.Equal(state.Cc, parsedState.Cc);
        Assert.Equal(state.Bcc, parsedState.Bcc);
        Assert.Equal(state.Subject, parsedState.Subject);
        Assert.Equal(state.Body, parsedState.Body);
    }

    [Fact]
    public void VCardBuilder_SerializesStructuredFieldsAndUnfoldsNotes()
    {
        VCardBuilderState state = new()
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Organization = "Analytical; Engines",
            MobilePhone = "+44, 123",
            Email = "ada@example.com",
            StreetAddress = "1 Logic Lane",
            Locality = "London",
            Country = "UK",
            Note = "First line\nSecond line",
        };

        string serialized = VCardBuilderHelper.Serialize(state);
        bool parsed = VCardBuilderHelper.TryParse(serialized, out VCardBuilderState parsedState);

        Assert.Equal(
            "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Ada Lovelace\r\nN:Lovelace;Ada;;;\r\nORG:Analytical\\; Engines\r\nTEL;TYPE=CELL:+44\\, 123\r\nEMAIL;TYPE=INTERNET:ada@example.com\r\nADR;TYPE=HOME:;;1 Logic Lane;London;;;UK\r\nNOTE:First line\\nSecond line\r\nEND:VCARD",
            serialized);
        Assert.True(parsed);
        Assert.Equal("Ada Lovelace", VCardBuilderHelper.GetDisplayName(parsedState));
        Assert.Equal(state.Organization, parsedState.Organization);
        Assert.Equal(state.MobilePhone, parsedState.MobilePhone);
        Assert.Equal(state.Country, parsedState.Country);
        Assert.Equal(state.Note, parsedState.Note);
    }

    [Theory]
    [InlineData(QrFramePreset.BottomLabel, "Scan Me")]
    [InlineData(QrFramePreset.RoundedFrame, "Scan Here")]
    [InlineData(QrFramePreset.CornerCallout, "Scan QR Code")]
    public void FramePresetCatalog_UsesStableDefaults(QrFramePreset preset, string defaultText)
    {
        Assert.Equal(defaultText, QrFramePresetCatalog.GetOption(preset).DefaultText);
        Assert.Equal(defaultText, QrFramePresetCatalog.ResolveText(preset, null));
        Assert.Equal("Custom label", QrFramePresetCatalog.ResolveText(preset, "  Custom label  "));
    }

    [Fact]
    public void FramePresetCatalog_SuppressesTextForNoFrame()
    {
        Assert.Equal(QrFramePreset.None, QrFramePresetCatalog.GetOption((QrFramePreset)999).Preset);
        Assert.Null(QrFramePresetCatalog.ResolveText(QrFramePreset.None, "Custom label"));
    }

    [Fact]
    public void MatrixEncoder_PreservesLatin1AndUsesUtf8ForUnicode()
    {
        QrMatrixEncoder encoder = new();

        QrMatrix latin1 = encoder.Encode("Crème brûlée", QrErrorCorrectionLevel.Medium);
        QrMatrix unicode = encoder.Encode("東京 😀", QrErrorCorrectionLevel.Medium);

        Assert.Equal(21, latin1.Size);
        Assert.Equal(21, unicode.Size);
        Assert.True(latin1[0, 0]);
        Assert.True(unicode[0, 0]);
    }

    [Fact]
    public void Renderer_ProducesPortablePngAndSelfContainedSvg()
    {
        QrRenderer renderer = new(new QrMatrixEncoder());
        QrRenderRequest request = QrRenderRequest.CreateDefault("https://example.com/東京");

        byte[] png = renderer.RenderPng(request);
        string svg = renderer.RenderSvg(request);

        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
        Assert.StartsWith("<svg ", svg, StringComparison.Ordinal);
        Assert.Contains("shape-rendering=\"crispEdges\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"http", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("href='http", svg, StringComparison.Ordinal);
    }
}
