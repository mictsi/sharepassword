namespace SharePassword.Options;

public class SqlServerStorageOptions
{
    public const string SectionName = "SqlServerStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}