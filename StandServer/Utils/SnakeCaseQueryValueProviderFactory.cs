using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StandServer.Utils;

public class SnakeCaseQueryValueProvider : QueryStringValueProvider
{
    public SnakeCaseQueryValueProvider(
        BindingSource bindingSource,
        IQueryCollection values,
        CultureInfo culture)
        : base(bindingSource, values, culture) { }

    public override bool ContainsPrefix(string prefix)
    {
        return base.ContainsPrefix(SnakeCaseNamingPolicy.FromPascalToSnakeCase(prefix));
    }

    public override ValueProviderResult GetValue(string key)
    {
        return base.GetValue(SnakeCaseNamingPolicy.FromPascalToSnakeCase(key));
    }
}

public class SnakeCaseQueryValueProviderFactory : IValueProviderFactory
{
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var valueProvider = new SnakeCaseQueryValueProvider(
            BindingSource.Query,
            context.ActionContext.HttpContext.Request.Query,
            CultureInfo.CurrentCulture);

        context.ValueProviders.Add(valueProvider);

        return Task.CompletedTask;
    }
}