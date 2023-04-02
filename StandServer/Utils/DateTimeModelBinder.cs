﻿using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StandServer.Utils;

public class DateTimeModelBinder : IModelBinder
{
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
            date = DateTime.UnixEpoch.AddMilliseconds(unix);
        else
        if (!DateTime.TryParse(dateStr, new CultureInfo("ru"), DateTimeStyles.None, out date))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "DateTime should be in format 'dd.MM.yyyy HH:mm:ss' or UNIX");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(date);
        return Task.CompletedTask;
    }
}

public class DateTimeModelBinderProvider : IModelBinderProvider
{
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