using System.ComponentModel.DataAnnotations;

namespace TVLauncher.App.Models;

/// <summary>Última tentativa de atualizar uma fonte externa, persistida entre aberturas.</summary>
public sealed class CatalogSyncCheckpoint
{
    [Key] [MaxLength(80)] public string Key { get; set; } = "watchmode";
    public long LastAttemptUnix { get; set; }
}
