using System.Text.RegularExpressions;

namespace StandServer.Models;

/// <summary> Model for the "users" table. </summary>
[Table("users")]
public class User
{
    /// <summary> User id. (PK) </summary>
    [Column("id")] public int Id { get; set; }

    /// <summary> User login. </summary>
    [Column("login")] public string Login { get; set; } = null!;

    /// <summary> User password. </summary>
    [Column("password")] public string Password { get; set; } = null!;

    /// <summary> Does the user have administrator rights. </summary>
    [Column("is_admin")] public bool IsAdmin { get; set; }

    /// <summary> Navigation property for telegram bot users who are authorized under this user. </summary>
    public List<TelegramBotUser> TelegramBotUsers { get; set; } = null!;
}

/// <summary> IServiceCollection.AddAuthorization policy names. </summary>
public static class AuthPolicy
{
    public const string Admin = nameof(Admin);
}

/// <summary> Model for login request. </summary>
public record LoginModel(string? Login, string? Password);

/// <summary> Validator for <see cref="LoginModel"/>. </summary>
public class LoginModelValidator : AbstractValidator<LoginModel>
{
    public LoginModelValidator()
    {
        RuleFor(user => user.Login).NotNull().NotEmpty();
        RuleFor(user => user.Password).NotNull();
    }
}

/// <summary> Model for register request. </summary>
public record RegisterModel(string? Login, string? Password, bool IsAdmin = false);

/// <summary> Validator for <see cref="RegisterModel"/>. </summary>
public class RegisterModelValidator : AbstractValidator<RegisterModel>
{
    private static readonly Regex LoginRegex = new(@"^[a-z][a-z0-9_\-]*$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public RegisterModelValidator()
    {
        RuleFor(user => user.Login).NotNull().NotEmpty().Length(1, 30).Matches(LoginRegex);
        RuleFor(user => user.Password).NotNull().Length(1, 30);
    }
}

/// <summary> Model for change password request. </summary>
public record ChangePasswordModel(string? NewPassword, string? OldPassword);

/// <summary> Validator for <see cref="ChangePasswordModel"/>. </summary>
public class ChangePasswordModelValidator : AbstractValidator<ChangePasswordModel>
{
    public ChangePasswordModelValidator()
    {
        RuleFor(user => user.NewPassword).NotNull().Length(1, 30);
        RuleFor(user => user.OldPassword).NotNull();
    }
}

/// <summary> Model for edit user request. </summary>
public class EditUserModel : PatchDtoBase
{
    private string? newPassword;
    private bool? isAdmin;

    /// <summary> If set, the user's password will be changed. </summary>
    public string? NewPassword
    {
        get => newPassword;
        set => SetField(ref newPassword, value);
    }

    /// <summary> If set, user rights will be changed. </summary>
    public bool? IsAdmin
    {
        get => isAdmin;
        set => SetField(ref isAdmin, value);
    }
}

/// <summary> Validator for <see cref="EditUserModel"/>. </summary>
public class EditUserModelValidator : AbstractValidator<EditUserModel>
{
    public EditUserModelValidator()
    {
        RuleFor(user => user.NewPassword).NotNull().Length(1, 30).WhenPropertyChanged();
        RuleFor(user => user.IsAdmin).NotNull().WhenPropertyChanged();
    }
}