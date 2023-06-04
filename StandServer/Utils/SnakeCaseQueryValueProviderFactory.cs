using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StandServer.Utils;

/// <summary> An snake case adapter for data stored in an <see cref="IQueryCollection"/>. </summary>
public class SnakeCaseQueryValueProvider : QueryStringValueProvider
{
    /// <inheritdoc />
    public SnakeCaseQueryValueProvider(
        BindingSource bindingSource,
        IQueryCollection values,
        CultureInfo culture)
        : base(bindingSource, values, culture) { }

    /// <summary> Determines whether the collection contains the specified prefix. </summary>
    public override bool ContainsPrefix(string prefix)
    {
        return base.ContainsPrefix(SnakeCaseNamingPolicy.FromPascalToSnakeCase(prefix));
    }

    /// <summary> Returns a value object using the specified key </summary>
    /// <param name="key">The key of the value object to retrieve.</param>
    /// <returns>The value object for the specified key.</returns>
    public override ValueProviderResult GetValue(string key)
    {
        return base.GetValue(SnakeCaseNamingPolicy.FromPascalToSnakeCase(key));
    }
}

/// <summary> A factory for creating <see cref="SnakeCaseQueryValueProvider"/> instances. </summary>
public class SnakeCaseQueryValueProviderFactory : IValueProviderFactory
{
    /// <summary>
    /// Creates a <see cref="SnakeCaseQueryValueProvider"/> with values from the current request
    /// and adds it to <see cref="ValueProviderFactoryContext.ValueProviders"/> list.
    /// </summary>
    /// <param name="context">The <see cref="ValueProviderFactoryContext"/>.</param>
    /// <returns>A <see cref="Task"/> that when completed will add an <see cref="SnakeCaseQueryValueProvider"/> instance
    /// to <see cref="ValueProviderFactoryContext.ValueProviders"/> list if applicable.</returns>
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var query = context.ActionContext.HttpContext.Request.Query;
        if (query.Count > 0)
        {
            var valueProvider = new SnakeCaseQueryValueProvider(
                BindingSource.Query,
                query,
                CultureInfo.CurrentCulture);

            context.ValueProviders.Add(valueProvider);
        }

        return Task.CompletedTask;
    }
}