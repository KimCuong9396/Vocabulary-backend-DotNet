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
[Authorize] // Yêu cầu xác thực cho tất cả endpoint
public class UserProgressController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserProgressController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/UserProgress/review/{progressId}
    [HttpPost("review/{progressId}")]
    public async Task<ActionResult<UserProgress>> ReviewWord(int progressId)
    {
        // Lấy UserId từ JWT
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Tìm bản ghi tiến trình
        var progress = await _context.UserProgresses
            .Include(up => up.Word)
            .FirstOrDefaultAsync(up => up.ProgressId == progressId);
        
        if (progress == null)
        {
            return NotFound(new { message = "Progress not found." });
        }

        // Kiểm tra xem bản ghi có thuộc về người dùng hiện tại không
        if (progress.UserId != currentUserId)
        {
            return Forbid(); // 403 Forbidden nếu không phải chủ sở hữu
        }

        // Cập nhật tiến trình (Golden Time logic)
        progress.MemoryLevel = Math.Min(progress.MemoryLevel + 1, 5);
        progress.LastReviewed = DateTime.UtcNow;
        progress.ReviewCount++;
        progress.NextReview = progress.MemoryLevel switch
        {
            1 => DateTime.UtcNow.AddDays(1),
            2 => DateTime.UtcNow.AddDays(3),
            3 => DateTime.UtcNow.AddDays(7),
            4 => DateTime.UtcNow.AddDays(14),
            _ => DateTime.UtcNow.AddDays(30)
        };
        progress.Status = progress.MemoryLevel == 5 ? "Mastered" : "Learning";

        await _context.SaveChangesAsync();

        return Ok(progress);
    }

    // GET: api/UserProgress/due
    [HttpGet("due")]
    public async Task<ActionResult<IEnumerable<UserProgress>>> GetDueWords()
    {
        // Lấy UserId từ JWT
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Lấy danh sách từ cần ôn tập
        var dueWords = await _context.UserProgresses
            .Where(up => up.UserId == currentUserId && up.NextReview <= DateTime.UtcNow)
            .Include(up => up.Word)
            .ThenInclude(w => w.Translations)
            .ToListAsync();

        return Ok(dueWords);
    }
}