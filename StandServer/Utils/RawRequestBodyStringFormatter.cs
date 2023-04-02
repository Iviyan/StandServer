using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace StandServer.Utils;

public class RawRequestBodyFormatter : InputFormatter
{
    public RawRequestBodyFormatter()
    {
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(MediaTypeNames.Text.Plain));
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(MediaTypeNames.Application.Octet));
    }

    public override bool CanRead(InputFormatterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (context.HttpContext.Request.ContentType == null) return true;
        
        if (!MediaTypeHeaderValue.TryParse(context.HttpContext.Request.ContentType, out var contentType)) return false;

        if (contentType.MediaType == MediaTypeNames.Text.Plain 
            || contentType.MediaType == MediaTypeNames.Application.Octet)
            return true;

        return false;
    }
    
    public override Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var request = context.HttpContext.Request;
        if (request.ContentLength == 0)
            return InputFormatterResult.SuccessAsync(
                context.ModelType == typeof(byte[]) ? Array.Empty<byte>() : String.Empty);

        return ReadRequestBodyAsync(context);
    }


    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;
        MediaTypeHeaderValue? contentType = null;
        
        if (context.HttpContext.Request.ContentType != null && 
            !MediaTypeHeaderValue.TryParse(context.HttpContext.Request.ContentType, out contentType))
            return await InputFormatterResult.FailureAsync();

        if (contentType == null || contentType.MediaType == MediaTypeNames.Text.Plain)
        {
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(content);
        }

        if (contentType.MediaType == MediaTypeNames.Application.Octet)
        {
            using var ms = new MemoryStream(2048);
            await request.Body.CopyToAsync(ms);
            var content = ms.ToArray();
            return await InputFormatterResult.SuccessAsync(content);
        }

        return await InputFormatterResult.FailureAsync();
    }
}