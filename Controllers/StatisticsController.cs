using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VocabularyApp.Data;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatisticsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Statistics
    [HttpGet]
    public async Task<ActionResult<StatisticsDto>> GetStatistics()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Tổng số từ đã học
        var totalWordsLearned = await _context.UserProgresses
            .CountAsync(up => up.UserId == currentUserId && up.Status != "NotLearned");

        // Số từ thành thạo (MemoryLevel = 5)
        var masteredWords = await _context.UserProgresses
            .CountAsync(up => up.UserId == currentUserId && up.MemoryLevel == 5);

        // Tỷ lệ thành thạo
        var masteryRate = totalWordsLearned > 0 ? (double)masteredWords / totalWordsLearned * 100 : 0;

        // Số từ cần ôn tập hôm nay
        var wordsDueToday = await _context.UserProgresses
            .CountAsync(up => up.UserId == currentUserId && up.NextReview <= DateTime.UtcNow);

        // Số bài kiểm tra đã hoàn thành
        var quizzesCompleted = await _context.QuizResults
            .CountAsync(qr => qr.UserId == currentUserId);

        // Điểm trung bình bài kiểm tra
        var averageQuizScore = await _context.QuizResults
            .Where(qr => qr.UserId == currentUserId)
            .AverageAsync(qr => (double?)qr.Score) ?? 0;

        var stats = new StatisticsDto
        {
            TotalWordsLearned = totalWordsLearned,
            MasteredWords = masteredWords,
            MasteryRate = Math.Round(masteryRate, 2),
            WordsDueToday = wordsDueToday,
            QuizzesCompleted = quizzesCompleted,
            AverageQuizScore = Math.Round(averageQuizScore, 2)
        };

        return Ok(stats);
    }
}

// DTO cho thống kê
public class StatisticsDto
{
    public int TotalWordsLearned { get; set; }
    public int MasteredWords { get; set; }
    public double MasteryRate { get; set; }
    public int WordsDueToday { get; set; }
    public int QuizzesCompleted { get; set; }
    public double AverageQuizScore { get; set; }
}