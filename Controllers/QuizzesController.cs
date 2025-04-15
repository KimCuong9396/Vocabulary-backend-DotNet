using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<QuizzesController> _logger;

    public QuizzesController(AppDbContext context, ILogger<QuizzesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Quizzes/lesson/5
    [HttpGet("lesson/{lessonId}")]
    public async Task<ActionResult<IEnumerable<Quiz>>> GetQuizzesByLesson(int lessonId)
    {
        try
        {
            _logger.LogInformation("Fetching quizzes for LessonId: {LessonId}", lessonId);
            var quizzes = await _context.Quizzes
                .Where(q => q.LessonId == lessonId)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} quizzes for LessonId: {LessonId}", quizzes.Count, lessonId);
            return Ok(quizzes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching quizzes for LessonId: {LessonId}", lessonId);
            return StatusCode(500, new { message = "An unexpected error occurred while fetching quizzes." });
        }
    }

    // GET: api/Quizzes/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Quiz>> GetQuiz(int id)
    {
        try
        {
            _logger.LogInformation("Fetching quiz with ID: {QuizId}", id);
            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                .FirstOrDefaultAsync(q => q.QuizId == id);

            if (quiz == null)
            {
                _logger.LogWarning("Quiz not found with ID: {QuizId}", id);
                return NotFound(new { message = "Quiz not found." });
            }

            _logger.LogInformation("Quiz retrieved successfully: {QuizId}", id);
            return Ok(quiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching quiz with ID: {QuizId}", id);
            return StatusCode(500, new { message = "An unexpected error occurred while fetching the quiz." });
        }
    }

    // POST: api/Quizzes
    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<Quiz>> CreateQuiz([FromBody] QuizRequest quizRequest)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received quiz creation request: {Request}", JsonConvert.SerializeObject(quizRequest));

            // Kiểm tra userIdClaim
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token");
                return Unauthorized(new { message = "Invalid token: Missing user ID." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { message = "Invalid token: User ID is not valid." });
            }

            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(quizRequest.Title))
            {
                _logger.LogWarning("Title is required for quiz creation");
                return BadRequest(new { message = "Title is required." });
            }

            if (quizRequest.LessonId <= 0)
            {
                _logger.LogWarning("Invalid LessonId: {LessonId}", quizRequest.LessonId);
                return BadRequest(new { message = "LessonId must be a positive integer." });
            }

            // Kiểm tra lesson tồn tại
            var lesson = await _context.Lessons
                .Include(l => l.LessonWords)
                .ThenInclude(lw => lw.Word)
                .ThenInclude(w => w.Translations)
                .FirstOrDefaultAsync(l => l.LessonId == quizRequest.LessonId);

            if (lesson == null)
            {
                _logger.LogWarning("Lesson not found with ID: {LessonId}", quizRequest.LessonId);
                return NotFound(new { message = "Lesson not found." });
            }

            // Kiểm tra xem lesson có từ vựng không
            if (!lesson.LessonWords.Any())
            {
                _logger.LogWarning("No words found for LessonId: {LessonId}", quizRequest.LessonId);
                return BadRequest(new { message = "No words available in this lesson to create a quiz." });
            }

            // Tạo quiz
            var quiz = new Quiz
            {
                LessonId = quizRequest.LessonId,
                Title = quizRequest.Title,
                Description = quizRequest.Description,
                QuizType = quizRequest.QuizType ?? "MultipleChoice", // Mặc định là trắc nghiệm
                CreatedAt = DateTime.UtcNow,
                Results = new List<QuizResult>()
            };

            // Tạo câu hỏi trắc nghiệm dựa trên từ vựng
            var questions = GenerateQuizQuestions(lesson.LessonWords.Select(lw => lw.Word).ToList());
            quiz.Description += $"\nGenerated {questions.Count} questions based on lesson words.";

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Quiz created successfully by UserId: {UserId}, QuizId: {QuizId}, LessonId: {LessonId}",
                userId, quiz.QuizId, quiz.LessonId);
            return CreatedAtAction(nameof(GetQuiz), new { id = quiz.QuizId }, new { quiz, questions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quiz by user with ID: {UserIdClaim}", userIdClaim ?? "unknown");
            return StatusCode(500, new { message = "An unexpected error occurred while creating the quiz." });
        }
    }

    // Hàm tạo câu hỏi trắc nghiệm
    private List<QuizQuestion> GenerateQuizQuestions(List<Word> words)
    {
        var random = new Random();
        var questions = new List<QuizQuestion>();
        var maxQuestions = Math.Min(words.Count, 5); // Tối đa 5 câu hỏi

        // Chỉ lấy các từ có ít nhất một bản dịch
        var validWords = words.Where(w => w.Translations.Any()).ToList();
        if (!validWords.Any())
        {
            // Nếu không có từ nào có bản dịch, tạo câu hỏi dựa trên WordText
            validWords = words;
        }

        var selectedWords = validWords.OrderBy(x => random.Next()).Take(maxQuestions).ToList();

        foreach (var word in selectedWords)
        {
            // Tạo câu hỏi trắc nghiệm: Chọn bản dịch đúng
            var correctTranslation = word.Translations.FirstOrDefault()?.Meaning ?? word.WordText;
            var incorrectOptions = validWords
                .Where(w => w.WordId != word.WordId)
                .OrderBy(x => random.Next())
                .Take(3)
                .Select(w => w.Translations.FirstOrDefault()?.Meaning ?? w.WordText)
                .ToList();

            // Nếu không đủ 3 lựa chọn sai, thêm một số tùy chọn mặc định
            while (incorrectOptions.Count < 3)
            {
                incorrectOptions.Add($"Option {incorrectOptions.Count + 1}");
            }

            var options = new List<string> { correctTranslation };
            options.AddRange(incorrectOptions);
            options = options.OrderBy(x => random.Next()).ToList();

            var question = new QuizQuestion
            {
                QuestionText = $"What is the meaning of '{word.WordText}'?",
                Options = options,
                CorrectAnswer = correctTranslation
            };

            questions.Add(question);
        }

        return questions;
    }
}

// DTO cho request tạo quiz
public class QuizRequest
{
    public int LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? QuizType { get; set; }
}

// Model cho câu hỏi trắc nghiệm (chỉ để trả về, không lưu vào DB)
public class QuizQuestion
{
    public string QuestionText { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = string.Empty;
}