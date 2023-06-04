using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace StandServer.Models;

/// <summary> Base class for PATCH method models. </summary>
public abstract class PatchDtoBase
{
    /// <summary> A list of properties that were passed in the request. </summary>
    public HashSet<string> ChangedProperties { get; } = new();

    /// <summary> Checks whether the <see cref="ChangedProperties"/> list
    /// contains this <paramref name="propertyName">property name</paramref>. </summary>
    public bool IsFieldPresent(string propertyName)
        => ChangedProperties.Contains(propertyName);

    /// <summary> Note that this property has been changed. </summary>
    protected void SetHasProperty([CallerMemberName] string propertyName = null!)
        => ChangedProperties.Add(propertyName);

    /// <summary>
    /// Set field value and mark it as changed. <br/>
    /// <example> public string? NewPassword { get => newPassword; set => SetField(ref newPassword, value); </example>
    /// </summary>
    protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        field = value;
        ChangedProperties.Add(propertyName!);
    }
}

/// <summary> Extension methods for <see cref="PatchDtoBase" /> class. </summary>
public static class PatchDtoBaseExtensions
{
    /// <summary> Strongly typed property change check. Not recommended. </summary>
    public static bool IsFieldPresent<T, TP>(this T dto, Expression<Func<T, TP>> expression) where T : PatchDtoBase =>
        expression.Body is MemberExpression exp
            ? dto.ChangedProperties.Contains(exp.Member.Name)
            : throw new ArgumentException("Expression is not a property.");

    /// <summary>
    /// Specifies a condition limiting when the validator should run.
    /// The validator will only be executed if the property was changed.
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WhenPropertyChanged<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule)
        where T : PatchDtoBase
        => rule.When((x, ctx) =>
            x.IsFieldPresent(CamelCaseNamingPolicy.FromCamelToPascalCase(ctx.PropertyName))
        );
}