
namespace Simple_QR_Code_Maker.Models;

public class RequestPaneChange
{
    public MainViewPanes Pane { get; set; }

    public PaneState RequestState { get; set; }

    public string SearchText { get; set; }

    public RequestPaneChange(MainViewPanes pane, PaneState requestState, string searchText = "")
    {
        Pane = pane;
        RequestState = requestState;
        SearchText = searchText;
    }
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