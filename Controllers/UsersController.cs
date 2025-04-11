using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Users/profile
    [HttpGet("profile")]
    public async Task<ActionResult<User>> GetProfile()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var user = await _context.Users
            .Select(u => new { u.UserId, u.Username, u.Email, u.FullName, u.PreferredLanguage, u.IsPremium })
            .FirstOrDefaultAsync(u => u.UserId == currentUserId);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(user);
    }

    // PUT: api/Users/profile
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.FullName = dto.FullName ?? user.FullName;
        user.PreferredLanguage = dto.PreferredLanguage ?? user.PreferredLanguage;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Profile updated successfully." });
    }
}

// DTO để cập nhật hồ sơ
public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? PreferredLanguage { get; set; }
}