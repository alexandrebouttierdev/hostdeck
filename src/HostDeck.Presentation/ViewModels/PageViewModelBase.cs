using CommunityToolkit.Mvvm.ComponentModel;

namespace HostDeck.Presentation.ViewModels;

/// <summary>Base commune des écrans : titre affiché dans le topbar et le fil d'Ariane.</summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    public abstract string Title { get; }

    public abstract string Breadcrumb { get; }

    public virtual string StatusSummary => string.Empty;
}
