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

    [HttpPut("profile")]
        public async Task<ActionResult<object>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                // Log thông tin request
                _logger.LogInformation("Received profile update request: {Request}", JsonConvert.SerializeObject(request));

                // Lấy userId từ token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                            ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Missing or empty user ID claim in JWT token");
                    return Unauthorized(new { Message = "Invalid token: Missing user ID." });
                }

                if (!int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                    return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
                }

                // Tìm user trong database
                var user = await _context.Users
                    .Where(u => u.UserId == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("No user found with UserId: {UserId}", userId);
                    return NotFound(new { Message = "User not found." });
                }

                // Cập nhật thông tin user
                if (!string.IsNullOrWhiteSpace(request.FullName))
                    user.FullName = request.FullName;
                    
                if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
                    user.PreferredLanguage = request.PreferredLanguage;
                    
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    // Kiểm tra email có hợp lệ không
                    if (!IsValidEmail(request.Email))
                    {
                        return BadRequest(new { Message = "Invalid email format." });
                    }
                    
                    // Kiểm tra email đã tồn tại chưa (nếu khác email hiện tại)
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

                // Lưu thay đổi vào database
                await _context.SaveChangesAsync();

                // Trả về thông tin đã cập nhật
                var updatedUser = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.FullName,
                    user.PreferredLanguage,
                    user.IsPremium
                };

                _logger.LogInformation("Profile updated successfully for UserId: {UserId}", userId);
                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user with ID: {UserIdClaim}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(500, new { Message = "An unexpected error occurred while updating the profile." });
            }
        }

        // Thêm class model cho request
        public class UpdateProfileRequest
        {
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? PreferredLanguage { get; set; }
        }

        // Thêm phương thức kiểm tra email hợp lệ
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
    [HttpPut("{userId}/make-premium")]
    [Authorize(Roles = "Premium")] // Chỉ admin (Premium) được gọi
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
}