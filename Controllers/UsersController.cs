using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}", userIdClaim);
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

    [HttpGet("learned-count")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<object>> GetLearnedCount()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username
                })
                .ToListAsync();

            var learnedCounts = await _context.UserProgresses
                .GroupBy(up => up.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LearnedCount = g.Count()
                })
                .ToListAsync();

            var result = users.Select(u => new
            {
                u.UserId,
                u.Username,
                LearnedCount = learnedCounts
                    .FirstOrDefault(lc => lc.UserId == u.UserId)?.LearnedCount ?? 0
            }).ToList();

            _logger.LogInformation("Retrieved learned count for {Count} users", users.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learned count");
            return StatusCode(500, new { Message = "An unexpected error occurred while retrieving learned count." });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<object>> GetAllUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.PreferredLanguage,
                    u.IsPremium
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} users", users.Count);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(500, new { Message = "An unexpected error occurred while retrieving users." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<object>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
            {
                _logger.LogWarning("Username or Email is empty in create user request.");
                return BadRequest(new { Message = "Username and Email are required." });
            }

            if (!IsValidEmail(request.Email))
            {
                _logger.LogWarning("Invalid email format: {Email}", request.Email);
                return BadRequest(new { Message = "Invalid email format." });
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Username {Username} or Email {Email} already exists.", request.Username, request.Email);
                return Conflict(new { Message = "Username or Email already in use." });
            }

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                FullName = request.FullName,
                PreferredLanguage = request.PreferredLanguage,
                IsPremium = request.IsPremium,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!") // Mật khẩu mặc định
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var createdUser = new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.FullName,
                user.PreferredLanguage,
                user.IsPremium
            };

            _logger.LogInformation("User {UserId} created successfully.", user.UserId);
            return CreatedAtAction(nameof(GetProfile), new { id = user.UserId }, createdUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with Username: {Username}", request.Username);
            return StatusCode(500, new { Message = "An unexpected error occurred while creating the user." });
        }
    }

    [HttpPut("{userId}")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<object>> UpdateUser(int userId, [FromBody] UpdateProfileRequest request)
    {
        try
        {
            _logger.LogInformation("Received profile update request for UserId: {UserId}", userId);

            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("No user found with UserId: {UserId}", userId);
                return NotFound(new { Message = "User not found." });
            }

            if (!string.IsNullOrWhiteSpace(request.FullName))
                user.FullName = request.FullName;

            if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
                user.PreferredLanguage = request.PreferredLanguage;

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (!IsValidEmail(request.Email))
                {
                    return BadRequest(new { Message = "Invalid email format." });
                }

                if (request.Email != user.Email)
                {
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != userId);

                    if (existingUser != null)
                    {
                        return Conflict(new { Message = "Email already in use by another account." });
                    }

                    user.Email = request.Email;
                }
            }

            await _context.SaveChangesAsync();

            var updatedUser = new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.FullName,
                user.PreferredLanguage,
                user.IsPremium
            };

            _logger.LogInformation("User updated successfully for UserId: {UserId}", userId);
            return Ok(updatedUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID: {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred while updating the user." });
        }
    }

    [HttpPut("{userId}/make-premium")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult> MakeUserPremium(int userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found with UserId: {UserId}", userId);
                return NotFound(new { Message = "User not found." });
            }

            if (user.IsPremium)
            {
                _logger.LogInformation("User {UserId} is already Premium.", userId);
                return Ok(new { Message = "User is already Premium." });
            }

            user.IsPremium = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated to Premium successfully.", userId);
            return Ok(new { Message = $"User {user.Username} is now Premium." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId} to Premium.", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred while updating the user role." });
        }
    }

    [HttpPut("{userId}/remove-premium")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult> RemoveUserPremium(int userId)
    {
        try
        {
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                  ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(currentUserIdClaim, out int currentUserId))
            {
                _logger.LogWarning("Invalid current user ID claim: {UserIdClaim}", currentUserIdClaim);
                return Unauthorized(new { Message = "Invalid token: Missing or invalid user ID." });
            }

            if (currentUserId == userId)
            {
                _logger.LogWarning("User {UserId} attempted to remove their own Premium status.", userId);
                return BadRequest(new { Message = "Cannot remove Premium status of the current user." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found with UserId: {UserId}", userId);
                return NotFound(new { Message = "User not found." });
            }

            if (!user.IsPremium)
            {
                _logger.LogInformation("User {UserId} is already Non-Premium.", userId);
                return Ok(new { Message = "User is already Non-Premium." });
            }

            user.IsPremium = false;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} Premium status removed successfully.", userId);
            return Ok(new { Message = $"User {user.Username} is no longer Premium." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing Premium status for user {UserId}.", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred while removing Premium status." });
        }
    }

    [HttpDelete("{userId}")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult> DeleteUser(int userId)
    {
        try
        {
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                  ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(currentUserIdClaim, out int currentUserId))
            {
                _logger.LogWarning("Invalid current user ID claim: {UserIdClaim}", currentUserIdClaim);
                return Unauthorized(new { Message = "Invalid token: Missing or invalid user ID." });
            }

            if (currentUserId == userId)
            {
                _logger.LogWarning("User {UserId} attempted to delete themselves.", userId);
                return BadRequest(new { Message = "Cannot delete the current user." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found with UserId: {UserId}", userId);
                return NotFound(new { Message = "User not found." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted successfully.", userId);
            return Ok(new { Message = $"User {user.Username} has been deleted." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}.", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred while deleting the user." });
        }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string? FullName { get; set; }
        public string? PreferredLanguage { get; set; }
        public bool IsPremium { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PreferredLanguage { get; set; }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}