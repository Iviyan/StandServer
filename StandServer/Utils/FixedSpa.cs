using Microsoft.AspNetCore.SpaServices.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace StandServer.Utils;

// All for the sake of 102-103 lines

public static class FixedSpaApplicationBuilderExtensions
{
    /// <summary>
    /// Handles all requests from this point in the middleware chain by returning
    /// the default page for the Single Page Application (SPA).
    ///
    /// This middleware should be placed late in the chain, so that other middleware
    /// for serving static files, MVC actions, etc., takes precedence.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
    /// <param name="configuration">
    /// This callback will be invoked so that additional middleware can be registered within
    /// the context of this SPA.
    /// </param>
    public static void UseFixedSpa(this IApplicationBuilder app, Action<ISpaBuilder> configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Use the options configured in DI (or blank if none was configured). We have to clone it
        // otherwise if you have multiple UseSpa calls, their configurations would interfere with one another.
        var optionsProvider = app.ApplicationServices.GetService<IOptions<SpaOptions>>()!;
        SpaOptions copyFromOptions = optionsProvider.Value;
        var options = new SpaOptions
        {
            DefaultPage = copyFromOptions.DefaultPage,
            PackageManagerCommand = copyFromOptions.PackageManagerCommand,
            DefaultPageStaticFileOptions = copyFromOptions.DefaultPageStaticFileOptions,
            SourcePath = copyFromOptions.SourcePath,
            DevServerPort = copyFromOptions.DevServerPort,
        };

        var spaBuilder = new DefaultFixedSpaBuilder(app, options);
        configuration.Invoke(spaBuilder);
        FixedSpaDefaultPageMiddleware.Attach(spaBuilder);
    }
}

public class DefaultFixedSpaBuilder : ISpaBuilder
{
    public IApplicationBuilder ApplicationBuilder { get; }

    public SpaOptions Options { get; }

    public DefaultFixedSpaBuilder(IApplicationBuilder applicationBuilder, SpaOptions options)
    {
        ApplicationBuilder = applicationBuilder
                             ?? throw new ArgumentNullException(nameof(applicationBuilder));

        Options = options
                  ?? throw new ArgumentNullException(nameof(options));
    }
}

public class FixedSpaDefaultPageMiddleware
{
    public static void Attach(ISpaBuilder spaBuilder)
    {
        if (spaBuilder == null)
        {
            throw new ArgumentNullException(nameof(spaBuilder));
        }

        var app = spaBuilder.ApplicationBuilder;
        var options = spaBuilder.Options;

        // Rewrite all requests to the default page
        app.Use((context, next) =>
        {
            // If we have an Endpoint, then this is a deferred match - just noop.
            if (context.GetEndpoint() != null)
            {
                return next(context);
            }

            context.Request.Path = options.DefaultPage;
            return next(context);
        });

        // Serve it as a static file
        // Developers who need to host more than one SPA with distinct default pages can
        // override the file provider
        app.UseSpaStaticFilesInternal(
            options.DefaultPageStaticFileOptions ?? new StaticFileOptions(),
            allowFallbackOnServingWebRootFiles: true);

        // If the default file didn't get served as a static file (usually because it was not
        // present on disk), the SPA is definitely not going to work.
        app.Use((context, next) =>
        {
            // If we have an Endpoint, then this is a deferred match - just noop.
            if (context.GetEndpoint() != null
                || !HttpMethods.IsGet(context.Request.Method)
                || !HttpMethods.IsHead(context.Request.Method))
            {
                return next(context);
            }

            var message = "The SPA default page middleware could not return the default page " +
                          $"'{options.DefaultPage}' because it was not found, and no other middleware " +
                          "handled the request.\n";

            // Try to clarify the common scenario where someone runs an application in
            // Production environment without first publishing the whole application
            // or at least building the SPA.
            var hostEnvironment = (IWebHostEnvironment?)context.RequestServices.GetService(typeof(IWebHostEnvironment));
            if (hostEnvironment != null && hostEnvironment.IsProduction())
            {
                message += "Your application is running in Production mode, so make sure it has " +
                           "been published, or that you have built your SPA manually. Alternatively you " +
                           "may wish to switch to the Development environment.\n";
            }

            throw new InvalidOperationException(message);
        });
    }
}

public static class FixedSpaStaticFilesExtensions
{
    internal static void UseSpaStaticFilesInternal(
        this IApplicationBuilder app,
        StaticFileOptions staticFileOptions,
        bool allowFallbackOnServingWebRootFiles)
    {
        if (staticFileOptions == null)
        {
            throw new ArgumentNullException(nameof(staticFileOptions));
        }

        // If the file provider was explicitly supplied, that takes precedence over any other
        // configured file provider. This is most useful if the application hosts multiple SPAs
        // (via multiple calls to UseSpa()), so each needs to serve its own separate static files
        // instead of using AddSpaStaticFiles/UseSpaStaticFiles.
        // But if no file provider was specified, try to get one from the DI config.
        if (staticFileOptions.FileProvider == null)
        {
            var shouldServeStaticFiles = ShouldServeStaticFiles(
                app,
                allowFallbackOnServingWebRootFiles,
                out var fileProviderOrDefault);
            if (shouldServeStaticFiles)
            {
                staticFileOptions.FileProvider = fileProviderOrDefault;
            }
            else
            {
                // The registered ISpaStaticFileProvider says we shouldn't
                // serve static files
                return;
            }
        }

        app.UseStaticFiles(staticFileOptions);
    }

    private static bool ShouldServeStaticFiles(
        IApplicationBuilder app,
        bool allowFallbackOnServingWebRootFiles,
        out IFileProvider? fileProviderOrDefault)
    {
        var spaStaticFilesService = app.ApplicationServices.GetService<ISpaStaticFileProvider>();
        if (spaStaticFilesService != null)
        {
            // If an ISpaStaticFileProvider was configured but it says no IFileProvider is available
            // (i.e., it supplies 'null'), this implies we should not serve any static files. This
            // is typically the case in development when SPA static files are being served from a
            // SPA development server (e.g., Angular CLI or create-react-app), in which case no
            // directory of prebuilt files will exist on disk.
            fileProviderOrDefault = spaStaticFilesService.FileProvider;
            return fileProviderOrDefault != null;
        }

        if (!allowFallbackOnServingWebRootFiles)
        {
            throw new InvalidOperationException($"To use UseSpaStaticFiles, you must " +
                                                $"first register an {nameof(ISpaStaticFileProvider)} in the service provider, typically " +
                                                $"by calling services.AddSpaStaticFiles.");
        }

        // Fall back on serving wwwroot
        fileProviderOrDefault = null;
        return true;
    }
}