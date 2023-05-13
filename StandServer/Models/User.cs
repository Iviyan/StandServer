namespace StandServer.Models;

[Table("users")]
public class User
{
    [Column("id")] public int Id { get; set; }
    [Column("login")] public string Login { get; set; } = null!;
    [Column("password")] public string Password { get; set; } = null!;
    [Column("is_admin")] public bool IsAdmin { get; set; }

    public List<TelegramBotUser> TelegramBotUsers { get; set; } = null!;
}

public static class AuthPolicy
{
    public const string Admin = nameof(Admin);
}

public record LoginModel(string? Login, string? Password);

public class LoginModelValidator : AbstractValidator<LoginModel>
{
    public LoginModelValidator()
    {
        RuleFor(user => user.Login).NotNull();
        RuleFor(user => user.Password).NotNull();
    }
}
public record RegisterModel(string? Login, string? Password, bool IsAdmin = false);

public class RegisterModelValidator : AbstractValidator<RegisterModel>
{
    public RegisterModelValidator()
    {
        RuleFor(user => user.Login).NotNull().Length(0, 30);
        RuleFor(user => user.Password).NotNull().Length(1, 30);
    }
}

public record ChangePasswordRequest(string? NewPassword, string? OldPassword);

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(user => user.NewPassword).NotNull().Length(1, 30);
        RuleFor(user => user.OldPassword).NotNull();
    }
}

public class EditUserRequest : PatchDtoBase
{
    private string? newPassword;
    private bool? isAdmin;
    public string? NewPassword { get => newPassword; set => SetField(ref newPassword, value); }
    public bool? IsAdmin { get => isAdmin; set => SetField(ref isAdmin, value); }
}

public class EditUserRequestValidator : AbstractValidator<EditUserRequest>
{
    public EditUserRequestValidator()
    {
        RuleFor(user => user.NewPassword).NotNull().Length(1, 30).WhenPropertyChanged();
        RuleFor(user => user.IsAdmin).NotNull().WhenPropertyChanged();
    }
}