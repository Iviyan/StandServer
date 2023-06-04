using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace StandServer.Utils;

/// <summary> Reads a string from the request body. </summary>
public class RawRequestBodyFormatter : InputFormatter
{
    /// <summary> Adding <see cref="MediaTypeNames.Text.Plain">MediaTypeNames.Text.Plain</see>
    /// and <see cref="MediaTypeNames.Application.Octet">MediaTypeNames.Application.Octet</see> support </summary>
    public RawRequestBodyFormatter()
    {
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(MediaTypeNames.Text.Plain));
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(MediaTypeNames.Application.Octet));
    }

    /// <summary> Determines whether this RawRequestBodyFormatter can deserialize an object of the context's ModelType. </summary>
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

    /// <summary>
    /// Reads an object from the request body. <br/>
    /// If the request body is empty, it immediately returns the result, otherwise it calls <see cref="RawRequestBodyFormatter.ReadRequestBodyAsync"/>
    /// </summary>
    /// <returns>A Task that on completion deserializes the request body.</returns>
    public override Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var request = context.HttpContext.Request;
        if (request.ContentLength == 0)
            return InputFormatterResult.SuccessAsync(
                context.ModelType == typeof(byte[]) ? Array.Empty<byte>() : String.Empty);

        return ReadRequestBodyAsync(context);
    }

    /// <summary> Reads an object from the request body. </summary>
    /// <returns>A Task that on completion deserializes the request body to string or byte[].</returns>
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