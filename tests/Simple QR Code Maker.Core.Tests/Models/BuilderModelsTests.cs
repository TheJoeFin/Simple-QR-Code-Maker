using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Models;

[TestClass]
public sealed class BuilderModelsTests
{
    [TestMethod]
    public void UrlCloneIsIndependentIncludingObservableQueryParameters()
    {
        UrlBuilderLine original = new()
        {
            Scheme = "https",
            Authority = "example.com",
            Path = "/雪",
            Fragment = "top",
            UsesAuthorityDelimiter = true,
            QueryParameters = [new("key", "value")],
        };
        UrlBuilderLine clone = original.Clone();
        Assert.AreNotSame(original, clone);
        Assert.AreNotSame(original.QueryParameters, clone.QueryParameters);
        Assert.AreNotSame(original.QueryParameters[0], clone.QueryParameters[0]);
        Assert.AreEqual(original.Scheme, clone.Scheme);
        Assert.AreEqual(original.Authority, clone.Authority);
        Assert.AreEqual(original.Path, clone.Path);
        Assert.AreEqual(original.Fragment, clone.Fragment);
        Assert.AreEqual(original.UsesAuthorityDelimiter, clone.UsesAuthorityDelimiter);
        clone.QueryParameters[0].Value = "changed";
        clone.QueryParameters.Add(new("new", "parameter"));
        Assert.AreEqual("value", original.QueryParameters[0].Value);
        Assert.AreEqual(1, original.QueryParameters.Count);
    }

    [TestMethod]
    public void QueryParameterNotifiesOnlyWhenValueChangesAndClonesIndependently()
    {
        UrlBuilderQueryParameter parameter = new("key", "value");
        List<string?> changes = [];
        parameter.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        parameter.Name = "key";
        parameter.Value = "new";
        CollectionAssert.AreEqual(new[] { "Value" }, changes);
        UrlBuilderQueryParameter clone = parameter.Clone();
        clone.Name = "other";
        Assert.AreEqual("key", parameter.Name);
        Assert.AreEqual("new", clone.Value);
    }

    [TestMethod]
    public void VCardCloneCopiesEveryFieldWithoutSharingMutableState()
    {
        VCardBuilderState original = new();
        foreach (System.Reflection.PropertyInfo property in typeof(VCardBuilderState).GetProperties())
            property.SetValue(original, property.Name + " 雪");
        VCardBuilderState clone = original.Clone();
        Assert.AreNotSame(original, clone);
        foreach (System.Reflection.PropertyInfo property in typeof(VCardBuilderState).GetProperties())
            Assert.AreEqual(property.GetValue(original), property.GetValue(clone), property.Name);
        clone.FormattedName = "changed";
        Assert.AreEqual("FormattedName 雪", original.FormattedName);
    }

    [TestMethod]
    public void WifiCloneCopiesAllFieldsIndependently()
    {
        WifiBuilderState original = new() { Ssid = "雪", Password = "secret", AuthenticationType = WifiAuthenticationType.WEP, IsHiddenNetwork = true };
        WifiBuilderState clone = original.Clone();
        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.Ssid, clone.Ssid);
        Assert.AreEqual(original.Password, clone.Password);
        Assert.AreEqual(original.AuthenticationType, clone.AuthenticationType);
        Assert.AreEqual(original.IsHiddenNetwork, clone.IsHiddenNetwork);
        clone.Password = "changed";
        Assert.AreEqual("secret", original.Password);
    }

    [TestMethod]
    public void IdDefaultsAndCatalogCoverAllSupportedFormats()
    {
        GeneratedIdOptions options = new();
        Assert.AreEqual(SpreadsheetGeneratedIdFormat.Guid, options.Format);
        Assert.AreEqual(12, options.NanoIdLength);
        Assert.AreEqual(string.Empty, options.Prefix);
        Assert.AreEqual(string.Empty, options.Suffix);
        CollectionAssert.AreEquivalent(Enum.GetValues<SpreadsheetGeneratedIdFormat>(), SpreadsheetGeneratedIdFormatOption.All.Select(static option => option.Format).ToArray());
        Assert.IsTrue(SpreadsheetGeneratedIdFormatOption.All.All(static option => !string.IsNullOrWhiteSpace(option.DisplayName)));
    }
}
