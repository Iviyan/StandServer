using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace StandServer.Models;

public abstract class PatchDtoBase
{
    public HashSet<string> ChangedProperties { get; } = new();

    public bool IsFieldPresent(string propertyName)
        => ChangedProperties.Contains(propertyName);

    protected void SetHasProperty([CallerMemberName] string propertyName = null!)
        => ChangedProperties.Add(propertyName);

    protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        field = value;
        ChangedProperties.Add(propertyName!);
    }
}

public static class PatchDtoBaseExtensions
{
    public static bool IsFieldPresent<T, TP>(this T dto, Expression<Func<T, TP>> expression) where T : PatchDtoBase =>
        expression.Body is MemberExpression exp
            ? dto.ChangedProperties.Contains(exp.Member.Name)
            : throw new ArgumentException("Expression is not a property.");

    public static IRuleBuilderOptions<T, TProperty> WhenPropertyChanged<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule)
        where T : PatchDtoBase
        => rule.When((x, ctx) =>
            x.IsFieldPresent(CamelCaseNamingPolicy.FromCamelToPascalCase(ctx.PropertyName))
        );
}