namespace SharePassword.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";
    public const string SqliteBackend = "sqlite";
    public const string SqlServerBackend = "sqlserver";
    public const string PostgresqlBackend = "postgresql";
    public const string AzureBackend = "azure";

    public string Backend { get; set; } = SqliteBackend;

    public static string NormalizeBackend(string? backend)
    {
        var normalized = (backend ?? SqliteBackend).Trim().ToLowerInvariant();

        return normalized switch
        {
            "" => SqliteBackend,
            "sqlite" or "sqllite" => SqliteBackend,
            "sqlserver" or "mssql" => SqlServerBackend,
            "postgresql" or "postgres" or "npgsql" => PostgresqlBackend,
            "azure" or "keyvault" or "azurekeyvault" => AzureBackend,
            _ => throw new InvalidOperationException($"Unsupported storage backend '{backend}'. Supported values are sqlite, sqlserver, postgresql, and azure.")
        };
    }
}