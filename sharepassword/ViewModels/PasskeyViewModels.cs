namespace SharePassword.ViewModels;

public class SecondFactorViewModel
{
    public string Username { get; set; } = string.Empty;
    public bool HasTotp { get; set; }
    public bool HasPasskeys { get; set; }
    public string? ReturnUrl { get; set; }
}

public class PasskeyLoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public bool HasTotp { get; set; }
    public string? ReturnUrl { get; set; }
}

public class PasskeyListItemViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}

public class PasskeyRegisterRequest
{
    public string Response { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public class PasskeyAssertionVerifyRequest
{
    public string Response { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
