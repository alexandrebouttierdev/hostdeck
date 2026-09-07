using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Fabrique utilisée uniquement par l'outillage `dotnet ef` pour générer les migrations.
///
/// Elle pointe vers un fichier jetable : générer une migration ne doit jamais toucher la base
/// réelle de l'utilisateur, ni dépendre de son existence.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HostDeckDbContext>
{
    public HostDeckDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HostDeckDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "hostdeck-design-time.db")}")
            .Options;

        return new HostDeckDbContext(options);
    }
}
