using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StandServer.Utils;

/// <summary> IModelBinder implementation for DateTime parsing from 'dd.MM.yyyy HH:mm:ss' or UNIX formats </summary>
public class DateTimeModelBinder : IModelBinder
{
    private static readonly CultureInfo RuCulture = new("ru");

    /// <summary> Attempts to bind a DateTime model. </summary>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
            throw new ArgumentNullException(nameof(bindingContext));

        // Try to fetch the value of the argument by name
        var modelName = bindingContext.ModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var dateStr = valueProviderResult.FirstValue;
        DateTime date;

        if (long.TryParse(dateStr, out long unix))
        {
            date = DateTime.UnixEpoch.AddMilliseconds(unix);
        }
        else if (!DateTime.TryParse(dateStr, RuCulture, DateTimeStyles.None, out date))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                "DateTime should be in format 'dd.MM.yyyy HH:mm:ss' or UNIX");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(date);
        return Task.CompletedTask;
    }
}

/// <summary> Creates <see cref="DateTimeModelBinder"/> instances.
/// Register <see cref="DateTimeModelBinderProvider"/> instances in <c>MvcOptions</c>. </summary>
public class DateTimeModelBinderProvider : IModelBinderProvider
{
    /// <summary> Creates a <see cref="DateTimeModelBinder"/> based on <see cref="ModelBinderProviderContext"/>. </summary>
    /// <param name="context">The <see cref="ModelBinderProviderContext"/>.</param>
    /// <returns>An <see cref="DateTimeModelBinder"/>.</returns>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (context.Metadata.ModelType == typeof(DateTime) ||
            context.Metadata.ModelType == typeof(DateTime?))
        {
            return new DateTimeModelBinder();
        }

        return null;
    }
}