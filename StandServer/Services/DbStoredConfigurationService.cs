using System.Reflection;

namespace StandServer.Services;

public class DbStoredConfigurationService
{
    private readonly ILogger<DbStoredConfigurationService> logger;
    private readonly ApplicationContext context;
    private readonly ApplicationConfiguration appConfiguration;

    public DbStoredConfigurationService(ILogger<DbStoredConfigurationService> logger, ApplicationContext context,
        ApplicationConfiguration appConfiguration)
    {
        this.logger = logger;
        this.context = context;
        this.appConfiguration = appConfiguration;
    }

    private static readonly Type ApplicationConfigurationType = typeof(ApplicationConfiguration);
    private static readonly PropertyInfo[] Properties;
    private static readonly string[] PropertyNames;

    internal static bool PropertyPredicate(PropertyInfo p) => p.CanRead && p.CanWrite;
    static DbStoredConfigurationService()
    {
        Type type = typeof(ApplicationConfiguration);
        Properties = type.GetProperties().Where(PropertyPredicate).ToArray();
        PropertyNames = Properties.Select(p => p.Name).ToArray();
    }

    public async Task LoadAllAsync()
    {
        logger.LogInformation("Load app configuration start");

        var dbConfiguration = await context.Configuration.AsNoTracking()
            .Where(e => PropertyNames.Contains(e.Key))
            .ToListAsync();

        foreach (var element in dbConfiguration)
        {
            var property = Properties.First(p => p.Name == element.Key);

            MethodInfo? convertMethod = ApplicationConfigurationType.GetMethod($"{property.Name}FromString", new[] { typeof(string) });

            if (convertMethod != null)
            {
                if (convertMethod.ReturnType != property.PropertyType)
                {
                    logger.LogError("Method '{MemberName}' must return '{PropertyType}' type ",
                        convertMethod.Name, property.PropertyType.Name);
                }
                else
                {
                    object? value = convertMethod.Invoke(appConfiguration, new object?[] { element.Value });
                    property.SetValue(appConfiguration, value);
                }
            }
            else
            {
                TypeConverter typeConverter = TypeDescriptor.GetConverter(property.PropertyType);

                object? value = null;
                bool success = false;

                try
                {
#pragma warning disable CS8604
                    value = typeConverter.ConvertFromInvariantString(element.Value); // It's work with nullable types and null strings
#pragma warning restore CS8604
                    success = true;
                }
                catch (Exception)
                {
                    logger.LogError(
                        "Error parsing the value ('{Value}') of the configuration property ('{Property}') from the database",
                        element.Value, element.Key);
                }

                if (success)
                {
                    property.SetValue(appConfiguration, value);
                }
            }
        }
        
        logger.LogInformation("Load app configuration end");
    }

    public Task SaveAsync() => SaveAsync(PropertyNames);

    public async Task SaveAsync(string[] propertyNames, CancellationToken ct = default)
    {
        var properties = Properties.Where(p => propertyNames.Contains(p.Name)).ToList();

        foreach (var property in properties)
        {
            object? value = property.GetValue(appConfiguration);
            string? sValue = null;
            
            MethodInfo? convertMethod = ApplicationConfigurationType.GetMethod($"{property.Name}ToString", new[] { property.PropertyType });

            if (convertMethod != null)
            {
                if (convertMethod.ReturnType != typeof(string))
                    logger.LogError("Method '{MemberName}' must return 'String' type ", convertMethod.Name);
                else
                    sValue = convertMethod.Invoke(appConfiguration, new object?[] { value }) as string;
            }
            
            sValue ??= property.GetValue(appConfiguration)?.ToString();
            
            int r = await context.Database.ExecuteSqlInterpolatedAsync($"""
                insert into configuration values ({property.Name},{sValue})
                on conflict (key) do update set value = excluded.value;
                """, cancellationToken: ct);
        }
    }
    
    public Task ApplyAndSaveAsync(ApplicationConfigurationPatch patch, CancellationToken ct = default)
    {
        patch.Apply(appConfiguration);
        return SaveAsync(patch.ChangedProperties.ToArray(), ct);
    }
}