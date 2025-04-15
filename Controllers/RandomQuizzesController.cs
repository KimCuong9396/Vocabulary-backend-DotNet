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
public class RandomQuizzesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RandomQuizzesController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/RandomQuizzes
    [HttpPost]
    public async Task<ActionResult<RandomQuizDto>> CreateRandomQuiz([FromBody] CreateRandomQuizDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Kiểm tra số lượng câu hỏi hợp lệ
        if (dto.QuestionCount <= 0 || dto.QuestionCount > 50)
        {
            return BadRequest(new { message = "Question count must be between 1 and 50." });
        }

        // Lấy danh sách từ vựng đã học
        var learnedWords = await _context.UserProgresses
            .Where(up => up.UserId == currentUserId && up.Status != "NotLearned")
            .Include(up => up.Word)
            .ThenInclude(w => w.Translations)
            .Select(up => up.Word)
            .ToListAsync();

        if (!learnedWords.Any())
        {
            return BadRequest(new { message = "No learned words available to create a quiz." });
        }

        // Chọn ngẫu nhiên từ vựng
        var random = new Random();
        var selectedWords = learnedWords
            .OrderBy(_ => random.Next())
            .Take(dto.QuestionCount)
            .ToList();

        // Tạo bài kiểm tra ngẫu nhiên
        var quiz = new Quiz
        {
            Title = $"Random Quiz - {DateTime.UtcNow:yyyyMMddHHmmss}",
            Description = $"Random {dto.QuizType} quiz with {dto.QuestionCount} questions",
            LessonId = 0, // Không gắn với bài học cụ thể
            QuizType = dto.QuizType
        };
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        // Tạo DTO trả về
        var quizDto = new RandomQuizDto
        {
            QuizId = quiz.QuizId,
            Title = quiz.Title,
            Description = quiz.Description,
            QuizType = quiz.QuizType,
            Words = selectedWords.Select(w => new WordsDto
            {
                WordId = w.WordId,
                WordText = w.WordText,
                Pronunciation = w.Pronunciation,
                Translations = w.Translations.Select(t => new WordTranslationsDto
                {
                    Language = t.Language,
                    Meaning = t.Meaning,
                    ExampleSentence = t.ExampleSentence
                }).ToList()
            }).ToList()
        };

        return Ok(quizDto);
    }
}

// DTOs cho bài kiểm tra ngẫu nhiên
public class CreateRandomQuizDto
{
    public int QuestionCount { get; set; } = 10;
    public string QuizType { get; set; } = "MultipleChoice"; // MultipleChoice, Flashcard, etc.
}

public class RandomQuizDto
{
    public int QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? QuizType { get; set; }
    public List<WordsDto> Words { get; set; } = new();
}

public class WordsDto
{
    public int WordId { get; set; }
    public string WordText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public List<WordTranslationsDto> Translations { get; set; } = new();
}

public class WordTranslationsDto
{
    public string Language { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? ExampleSentence { get; set; }
}