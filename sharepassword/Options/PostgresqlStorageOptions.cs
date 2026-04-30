namespace SharePassword.Options;

public class PostgresqlStorageOptions
{
    public const string SectionName = "PostgresqlStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}