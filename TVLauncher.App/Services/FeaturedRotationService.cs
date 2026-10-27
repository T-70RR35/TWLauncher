using System.Security.Cryptography;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

/// <summary>
/// Embaralha a lista somente ao entrar na Home. Próximo e Anterior usam os
/// índices da fila gerada, sem consultas ao SQLite a cada mudança de destaque.
/// </summary>
public sealed class FeaturedRotationService
{
    private string[] _previousOrder = [];

    public IReadOnlyList<FeaturedTitle> ShuffleForHome(IReadOnlyList<FeaturedTitle> candidates)
    {
        var items = candidates.DistinctBy(x => x.RotationKey).ToArray();
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        // Evita a mesma ordem exata em duas entradas consecutivas.
        if (items.Length > 1 && items.Select(x => x.RotationKey).SequenceEqual(_previousOrder))
            (items[0], items[1]) = (items[1], items[0]);
        _previousOrder = items.Select(x => x.RotationKey).ToArray();
        return items;
    }
}
