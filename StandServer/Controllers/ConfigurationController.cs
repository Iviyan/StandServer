using StandServer.Services;

namespace StandServer.Controllers;

/// <summary> A controller containing actions for managing <see cref="ApplicationConfiguration"/>. </summary>
[ApiController]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> logger;

    public ConfigurationController(ILogger<ConfigurationController> logger) => this.logger = logger;

    /// <summary> A user-accessible GET method for getting the <see cref="ApplicationConfiguration">application configuration</see>. </summary>
    [HttpGet("configuration"), Authorize]
    public IActionResult GetConfiguration([FromServices] IApplicationConfiguration appConfiguration)
        => Ok(appConfiguration);

    /// <summary> Aa administrator-accessible PATCH method that
    /// edits the <see cref="ApplicationConfiguration">application configuration</see>. </summary>
    [HttpPatch("configuration"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> EditConfiguration(
        [FromBody] ApplicationConfigurationPatch patch,
        [FromServices] DbStoredConfigurationService dbStoredConfigurationService,
        [FromServices] IApplicationConfiguration appConfiguration)
    {
        await dbStoredConfigurationService.ApplyAndSaveAsync(patch);
        return Ok(appConfiguration);
    }
}