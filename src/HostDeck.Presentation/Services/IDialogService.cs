using System.Threading;
using System.Threading.Tasks;

namespace HostDeck.Presentation.Services;

/// <summary>Dialogues Presentation sans coupler les ViewModels à <c>Window</c> (§62).</summary>
public interface IDialogService
{
    /// <summary>Ouvre le formulaire d'ajout d'hôte. Renvoie <c>true</c> si un serveur a été enregistré.</summary>
    Task<bool> ShowAddHostAsync(CancellationToken cancellationToken = default);
}
