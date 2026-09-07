namespace HostDeck.Presentation.Navigation;

/// <summary>
/// Destinations du rail principal. La navigation reste une clé discrète, sans routes web (§63).
/// </summary>
public enum ShellSection
{
    Overview = 0,
    Infrastructure = 1,
    HostDetails = 2,
    Incidents = 3,
    LiveData = 4,
    Docker = 5,
    Alerts = 6,
    Reports = 7,
    Topology = 8,
    Settings = 9,
}
