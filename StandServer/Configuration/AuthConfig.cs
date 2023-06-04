namespace StandServer.Configuration;

/// <summary> Model for notification configuration. </summary>
public class AuthConfig
{
    /// <summary> Section name in JSON. </summary>
    public const string SectionName = "Auth";

    /// <summary> First user (administrator) configuration. </summary>
    public FirstUserConfig FirstUser { get; set; } = null!;

    /// <summary> JWT configuration. </summary>
    public JwtConfig Jwt { get; set; } = null!;

    /// <summary> Tokens lifetime configuration. </summary>
    public TokenTTLConfig TokenTTL { get; set; } = null!;
}

/// <summary> Model for JWT configuration. </summary>
public class JwtConfig
{
    /// <summary> Section name in JSON. </summary>
    public const string SectionName = "Jwt";

    /// <summary> JWT secret key. </summary>
    public string Secret { get; set; } = null!;

    /// <summary> JWT issuer. </summary>
    public string Issuer { get; set; } = null!;

    /// <summary> Getter to create <see cref="SymmetricSecurityKey"/> from <see cref="Secret"/>. </summary>
    public SymmetricSecurityKey SecretKey => new(Encoding.UTF8.GetBytes(Secret));
}

/// <summary> Model for first user (administrator) configuration. </summary>
public class FirstUserConfig
{
    /// <summary> Section name in JSON. </summary>
    public const string SectionName = "FirstUser";

    /// <summary> First user login. </summary>
    public string Login { get; set; } = null!;

    /// <summary> First user password. </summary>
    public string Password { get; set; } = null!;
}

/// <summary> Model for tokens lifetime configuration. </summary>
public class TokenTTLConfig
{
    /// <summary> Section name in JSON. </summary>
    public const string SectionName = "TokenTTL";

    /// <summary> Access token TTL in seconds </summary>
    public int Access { get; set; }

    /// <summary> Refresh token TTL in seconds </summary>
    public int Refresh { get; set; }
}