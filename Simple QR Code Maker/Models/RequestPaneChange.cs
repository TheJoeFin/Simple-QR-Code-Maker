
namespace Simple_QR_Code_Maker.Models;

public class RequestPaneChange
{
    public MainViewPanes Pane { get; set; }

    public PaneState RequestState { get; set; }

    public string SearchText { get; set; }

    public FaqTopic Topic { get; }

    public RequestPaneChange(MainViewPanes pane, PaneState requestState, string searchText = "", FaqTopic topic = FaqTopic.None)
    {
        Pane = pane;
        RequestState = requestState;
        SearchText = searchText;
        Topic = topic;
    }
}

[Flags]
public enum FaqTopic
{
    None = 0,
    Size = 1,
    ErrorCorrection = 2,
    Brands = 4,
    Detection = 8
}

public enum MainViewPanes
{
    History,
    Faq
}

public enum PaneState
{
    Open,
    Close
}