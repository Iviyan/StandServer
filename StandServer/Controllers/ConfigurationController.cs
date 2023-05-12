using StandServer.Services;

namespace StandServer.Controllers;

[ApiController]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> logger;

    public ConfigurationController(ILogger<ConfigurationController> logger) => this.logger = logger;
    
    [HttpGet("configuration"), Authorize]
    public IActionResult GetConfiguration([FromServices] IApplicationConfiguration appConfiguration) 
        => Ok(appConfiguration);

    [HttpPatch("configuration"), Authorize]
    public async Task<IActionResult> EditConfiguration(
        [FromBody] ApplicationConfigurationPatch patch,
        [FromServices] DbStoredConfigurationService dbStoredConfigurationService,
        [FromServices] IApplicationConfiguration appConfiguration)
    {
        await dbStoredConfigurationService.ApplyAndSaveAsync(patch);
        return Ok(appConfiguration);
    }
}