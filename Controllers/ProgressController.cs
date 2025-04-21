using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VocabularyApp.Data;
using VocabularyApp.Models;
using VocabularyApp.Models.Dtos;
using System.ComponentModel.DataAnnotations;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProgressController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProgressController> _logger;

    public ProgressController(AppDbContext context, ILogger<ProgressController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/progress/lesson/{lessonId}
    [HttpGet("lesson/{lessonId}")]
    public async Task<ActionResult<IEnumerable<UserProgressDto>>> GetProgressByLesson(int lessonId)
    {
        var userIdClaim = GetUserIdClaim();
        try
        {
            _logger.LogInformation("Fetching progress for LessonId: {LessonId}", lessonId);

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

            var lessonExists = await _context.Lessons.AnyAsync(l => l.LessonId == lessonId);
            if (!lessonExists)
            {
                _logger.LogWarning("Lesson not found with ID: {LessonId}", lessonId);
                return NotFound(new { Message = "Lesson not found." });
            }

            var progress = await _context.UserProgresses
                .Where(p => p.UserId == userId &&
                           _context.LessonWords.Any(lw => lw.LessonId == lessonId && lw.WordId == p.WordId))
                .Select(p => new UserProgressDto
                {
                    ProgressId = p.ProgressId,
                    UserId = p.UserId,
                    WordId = p.WordId,
                    MemoryLevel = p.MemoryLevel,
                    LastReviewed = p.LastReviewed,
                    NextReview = p.NextReview,
                    ReviewCount = p.ReviewCount,
                    Status = p.Status,
                    Word = new WordDto
                    {
                        WordId = p.Word.WordId,
                        WordText = p.Word.WordText,
                        Example = p.Word.Example,
                        Mean = p.Word.Mean,
                        Pronunciation = p.Word.Pronunciation,
                        Translations = p.Word.Translations.Select(t => new TranslationDto
                        {
                            TranslationId = t.TranslationId,
                            Language = t.Language,
                            Meaning = t.Meaning
                        }).ToList()
                    }
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} progress records for UserId: {UserId}, LessonId: {LessonId}", progress.Count, userId, lessonId);
            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching progress for UserId: {UserIdClaim}, LessonId: {LessonId}", userIdClaim ?? "unknown", lessonId);
            return StatusCode(500, new { Message = "An unexpected error occurred while fetching progress." });
        }
    }

    // GET: api/progress/learned-count
[HttpGet("learned-count")]
[Authorize(Roles = "Premium")] // Chỉ người dùng Premium được truy cập
public async Task<ActionResult<IEnumerable<LearnedCountDto>>> GetLearnedCounts()
{
    try
    {
        _logger.LogInformation("Fetching learned counts for all users");

        var learnedCounts = await _context.Users
            .Select(u => new LearnedCountDto
            {
                UserId = u.UserId,
                Username = u.Username,
                LearnedCount = _context.UserProgresses
                    .Count(p => p.UserId == u.UserId && p.Status == "Learned")
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} learned count records", learnedCounts.Count);
        return Ok(new { Values = learnedCounts }); // Trả về dưới dạng { Values: [...] } để khớp với frontend
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching learned counts");
        return StatusCode(500, new { Message = "An unexpected error occurred while fetching learned counts." });
    }
}

// DTO cho learned-count
public class LearnedCountDto
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public int LearnedCount { get; set; }
}
    
    // POST: api/progress
    [HttpPost]
    public async Task<ActionResult<UserProgressDto>> UpdateProgress([FromBody] UserProgressRequest request)
    {
        var userIdClaim = GetUserIdClaim();
        try
        {
            _logger.LogInformation("Received progress update request for WordId: {WordId}", request.WordId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for progress update: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

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

            var wordExists = await _context.Words.AnyAsync(w => w.WordId == request.WordId);
            if (!wordExists)
            {
                _logger.LogWarning("Word not found with ID: {WordId}", request.WordId);
                return NotFound(new { Message = "Word not found." });
            }

            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    UserId = userId,
                    WordId = request.WordId,
                    MemoryLevel = request.MemoryLevel,
                    LastReviewed = string.IsNullOrEmpty(request.LastReviewed) ? null : DateTime.Parse(request.LastReviewed),
                    NextReview = string.IsNullOrEmpty(request.NextReview) ? null : DateTime.Parse(request.NextReview),
                    ReviewCount = request.ReviewCount,
                    Status = request.Status
                };
                _context.UserProgresses.Add(progress);
                _logger.LogInformation("Created new progress for UserId: {UserId}, WordId: {WordId}", userId, request.WordId);
            }
            else
            {
                progress.MemoryLevel = request.MemoryLevel;
                progress.LastReviewed = string.IsNullOrEmpty(request.LastReviewed) ? null : DateTime.Parse(request.LastReviewed);
                progress.NextReview = string.IsNullOrEmpty(request.NextReview) ? null : DateTime.Parse(request.NextReview);
                progress.ReviewCount = request.ReviewCount;
                progress.Status = request.Status;
                _logger.LogInformation("Updated existing progress for UserId: {UserId}, WordId: {WordId}", userId, request.WordId);
            }

            await _context.SaveChangesAsync();

            var progressDto = new UserProgressDto
            {
                ProgressId = progress.ProgressId,
                UserId = progress.UserId,
                WordId = progress.WordId,
                MemoryLevel = progress.MemoryLevel,
                LastReviewed = progress.LastReviewed,
                NextReview = progress.NextReview,
                ReviewCount = progress.ReviewCount,
                Status = progress.Status,
                Word = await _context.Words
                    .Where(w => w.WordId == progress.WordId)
                    .Select(w => new WordDto
                    {
                        WordId = w.WordId,
                        WordText = w.WordText,
                        Example = w.Example,
                        Mean = w.Mean,
                        Pronunciation = w.Pronunciation,
                        Translations = w.Translations.Select(t => new TranslationDto
                        {
                          TranslationId = t.TranslationId,
                            Language = t.Language,
                            Meaning = t.Meaning
                        }).ToList()
                    })
                    .FirstOrDefaultAsync()
            };

            _logger.LogInformation("Progress updated successfully for UserId: {UserId}, WordId: {WordId}", userId, request.WordId);
            return Ok(progressDto);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid date format in progress update for UserId: {UserIdClaim}, WordId: {WordId}", userIdClaim ?? "unknown", request.WordId);
            return BadRequest(new { Message = "Invalid date format for LastReviewed or NextReview." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for UserId: {UserIdClaim}, WordId: {WordId}", userIdClaim ?? "unknown", request.WordId);
            return StatusCode(500, new { Message = "An unexpected error occurred while updating progress." });
        }
    }

    // GET: api/progress/learned
    [HttpGet("learned")]
    public async Task<ActionResult<IEnumerable<UserProgressDto>>> GetAllLearnedProgress()
    {
        var userIdClaim = GetUserIdClaim();
        try
        {
            _logger.LogInformation("Fetching all learned progress for UserId: {UserIdClaim}", userIdClaim);

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

            var progress = await _context.UserProgresses
                .Where(p => p.UserId == userId && p.Status == "Learned")
                .Select(p => new UserProgressDto
                {
                    ProgressId = p.ProgressId,
                    UserId = p.UserId,
                    WordId = p.WordId,
                    MemoryLevel = p.MemoryLevel,
                    LastReviewed = p.LastReviewed,
                    NextReview = p.NextReview,
                    ReviewCount = p.ReviewCount,
                    Status = p.Status,
                    Word = new WordDto
                    {
                        WordId = p.Word.WordId,
                        WordText = p.Word.WordText,
                        Example = p.Word.Example,
                        Mean=p.Word.Mean,
                        Pronunciation = p.Word.Pronunciation,
                        Translations = p.Word.Translations.Select(t => new TranslationDto
                        {
                            TranslationId = t.TranslationId,
                            Language = t.Language,
                            Meaning = t.Meaning
                        }).ToList()
                    }
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} learned progress records for UserId: {UserId}", progress.Count, userId);
            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching learned progress for UserId: {UserIdClaim}", userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while fetching learned progress." });
        }
    }

    private string? GetUserIdClaim()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst("sub")?.Value;
    }

    
}

