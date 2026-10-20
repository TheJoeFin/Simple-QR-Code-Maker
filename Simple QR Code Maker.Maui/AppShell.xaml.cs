namespace Simple_QR_Code_Maker.Maui;

public partial class AppShell : Shell
{
    public AppShell(MainPage mainPage, HistoryPage historyPage, DesignsPage designsPage)
    {
        InitializeComponent();
        TabBar destinations = new();
        destinations.Items.Add(CreateDestination("Create", nameof(MainPage), mainPage));
        destinations.Items.Add(CreateDestination("History", nameof(HistoryPage), historyPage));
        destinations.Items.Add(CreateDestination("Designs", nameof(DesignsPage), designsPage));
        Items.Add(destinations);
    }

    private static ShellContent CreateDestination(string title, string route, Page page) =>
        new()
        {
            Title = title,
            Route = route,
            Content = page,
        };
}
