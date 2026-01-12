using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace WorkoutService.Controllers;

[ApiController]
public class DevAuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public DevAuthController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public record DevTokenRequest(string UserId);

    [AllowAnonymous]
    [HttpPost("/dev/token")]
    public IActionResult CreateToken([FromBody] DevTokenRequest request)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("UserId is required");

        var jwtSection = _config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var key = jwtSection["Key"];

        var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, request.UserId),
    new Claim("sub", request.UserId),
    new Claim("userId", request.UserId)
};

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}