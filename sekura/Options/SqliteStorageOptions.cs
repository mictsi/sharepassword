namespace Sekura.Options;

public class SqliteStorageOptions
{
    public const string SectionName = "SqliteStorage";

    public string ConnectionString { get; set; } = "Data Source=App_Data/sekura.db";
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}