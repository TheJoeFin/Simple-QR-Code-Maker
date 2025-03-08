

namespace Simple_QR_Code_Maker.Models;

public class RequestPaneChange
{
    public MainViewPanes Pane { get; set; }

    public PaneState RequestState { get; set; }

    public RequestPaneChange(MainViewPanes pane, PaneState requestState)
    {
        Pane = pane;
        RequestState = requestState;
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