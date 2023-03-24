namespace StandServer.Configuration;

public class AuthConfig
{
    public const string SectionName = "Auth";
    public FirstUserConfig FirstUser { get; set; } = null!;
    public JwtConfig Jwt { get; set; } = null!;
    public TokenTTLConfig TokenTTL { get; set; } = null!;
}

public class JwtConfig
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = null!;
    public string Issuer { get; set; } = null!;

    public SymmetricSecurityKey SecretKey => new(Encoding.UTF8.GetBytes(Secret));
}

public class FirstUserConfig
{
    public const string SectionName = "FirstUser";
    
    public string Login { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class TokenTTLConfig
{
    public const string SectionName = "TokenTTL";
    
    /// <summary> Access token TTL in seconds </summary>
    public int Access { get; set; }
    
    /// <summary> Refresh token TTL in seconds </summary>
    public int Refresh { get; set; }
}