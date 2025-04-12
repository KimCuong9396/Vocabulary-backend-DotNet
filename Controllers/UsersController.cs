using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Security.Claims;
using Newtonsoft.Json;
using VocabularyApp.Data;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AppDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<object>> GetProfile()
    {
        try
        {
            // Log Authorization header
            var authHeader = Request.Headers["Authorization"].ToString();
            _logger.LogDebug("Authorization header: {AuthHeader}", authHeader.Length > 50 ? "[Truncated]" : authHeader);

            // Log tất cả claims
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            _logger.LogInformation("User claims: {Claims}", claims.Any() ? JsonConvert.SerializeObject(claims, Formatting.Indented) : "None");

            // Thử truy cập user ID từ nameidentifier hoặc sub
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token. Claims available: {Claims}", claims.Any() ? JsonConvert.SerializeObject(claims, Formatting.Indented) : "None");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
            }

            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.PreferredLanguage,
                    u.IsPremium
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("No user found with UserId: {UserId}", userId);
                return NotFound(new { Message = "User not found." });
            }

            _logger.LogInformation("Profile retrieved successfully for UserId: {UserId}", userId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for user with user ID claim: {UserIdClaim}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { Message = "An unexpected error occurred while retrieving the profile." });
        }
    }
}