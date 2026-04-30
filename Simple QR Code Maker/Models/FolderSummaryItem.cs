namespace Simple_QR_Code_Maker.Models;

public class FolderSummaryItem
{
    public string FileName { get; set; } = string.Empty;
    public int QrCodeCount { get; set; }
    public string QrCodeContents { get; set; } = string.Empty;
}

public class FolderSummaryNavigationParameter
{
    public string FolderName { get; set; } = string.Empty;
    public List<FolderSummaryItem> Items { get; set; } = [];
}
