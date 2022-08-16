using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace StandServer.Utils;

public class RawRequestBodyFormatter : InputFormatter
{
    public RawRequestBodyFormatter()
    {
        SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/plain"));
        SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
    }

    public override bool CanRead(InputFormatterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var contentType = context.HttpContext.Request.ContentType;
        if (string.IsNullOrEmpty(contentType) || contentType == "text/plain" ||
            contentType == "application/octet-stream")
            return true;
            
        return false;
    }

        
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;
        var contentType = context.HttpContext.Request.ContentType;


        if (string.IsNullOrEmpty(contentType) || contentType == "text/plain")
        {
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(content);
        }
        if (contentType == "application/octet-stream")
        {
            using var ms = new MemoryStream(2048);
            await request.Body.CopyToAsync(ms);
            var content = ms.ToArray();
            return await InputFormatterResult.SuccessAsync(content);
        }

        return await InputFormatterResult.FailureAsync();
    }
}

public static class HttpRequestExtensions
{
    /// <summary>
    /// Retrieve the raw body as a string from the Request.Body stream
    /// </summary>
    /// <param name="request">Request instance to apply to</param>
    /// <param name="encoding">Optional - Encoding, defaults to UTF8</param>
    /// <param name="inputStream">Optional - Pass in the stream to retrieve from. Other Request.Body</param>
    /// <returns></returns>
    public static async Task<string> GetRawBodyStringAsync(this HttpRequest request, Encoding? encoding = null, Stream? inputStream = null)
    {
        if (encoding == null)
            encoding = Encoding.UTF8;

        if (inputStream == null)
            inputStream = request.Body;

        using StreamReader reader = new StreamReader(inputStream, encoding);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Retrieves the raw body as a byte array from the Request.Body stream
    /// </summary>
    /// <param name="request"></param>
    /// <param name="inputStream"></param>
    /// <returns></returns>
    public static async Task<byte[]> GetRawBodyBytesAsync(this HttpRequest request, Stream? inputStream = null)
    {
        if (inputStream == null)
            inputStream = request.Body;

        using var ms = new MemoryStream(2048);
        await inputStream.CopyToAsync(ms);
        return ms.ToArray();
    }
}