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
public class QuizResultsController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuizResultsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/QuizResults
    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuizResult>>> GetQuizResults()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var results = await _context.QuizResults
            .Where(qr => qr.UserId == currentUserId)
            .Include(qr => qr.Quiz)
            .ThenInclude(q => q.Lesson)
            .OrderByDescending(qr => qr.CompletedAt)
            .ToListAsync();

        return Ok(results);
    }

    // POST: api/QuizResults
    [HttpPost]
    public async Task<ActionResult<QuizResult>> SubmitQuizResult([FromBody] SubmitQuizResultDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var quiz = await _context.Quizzes.FindAsync(dto.QuizId);
        if (quiz == null)
        {
            return NotFound(new { message = "Quiz not found." });
        }

        var result = new QuizResult
        {
            UserId = currentUserId,
            QuizId = dto.QuizId,
            Score = dto.Score
        };

        _context.QuizResults.Add(result);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetQuizResults), new { id = result.ResultId }, result);
    }
}

// DTO để gửi kết quả kiểm tra
public class SubmitQuizResultDto
{
    public int QuizId { get; set; }
    public int Score { get; set; }
}