using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CloudinaryController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public CloudinaryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("signature")]
    public IActionResult GetSignature()
    {
        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            return StatusCode(500, new { message = "Cloudinary configuration is missing." });
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signatureString = $"timestamp={timestamp}{apiSecret}";
        
        var bytes = Encoding.UTF8.GetBytes(signatureString);
        var hash = SHA1.HashData(bytes);
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return Ok(new
        {
            timestamp = timestamp.ToString(),
            signature,
            apiKey,
            cloudName
        });
    }
}
