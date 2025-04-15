using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VocabularyApp.Data;
using VocabularyApp.Models;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WordsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<WordsController> _logger;

    public WordsController(AppDbContext context, ILogger<WordsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Words
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Word>>> GetWords()
    {
        try
        {
            _logger.LogInformation("Fetching all words");
            var words = await _context.Words
                .Include(w => w.Translations)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} words", words.Count);
            return Ok(words);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching words");
            return StatusCode(500, new { Message = "An unexpected error occurred while fetching words." });
        }
    }

    // GET: api/Words/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Word>> GetWord(int id)
    {
        try
        {
            _logger.LogInformation("Fetching word with ID: {WordId}", id);
            var word = await _context.Words
                .Include(w => w.Translations)
                .FirstOrDefaultAsync(w => w.WordId == id);
            if (word == null)
            {
                _logger.LogWarning("Word not found with ID: {WordId}", id);
                return NotFound(new { Message = "Word not found." });
            }
            _logger.LogInformation("Word retrieved successfully: {WordId}", id);
            return Ok(word);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching word with ID: {WordId}", id);
            return StatusCode(500, new { Message = "An unexpected error occurred while fetching the word." });
        }
    }

    // GET: api/Words/lesson/5
    [HttpGet("lesson/{lessonId}")]
    public async Task<ActionResult<IEnumerable<Word>>> GetWordsByLesson(int lessonId)
    {
        try
        {
            _logger.LogInformation("Fetching words for LessonId: {LessonId}", lessonId);

            // Kiểm tra lesson có tồn tại không
            var lessonExists = await _context.Lessons.AnyAsync(l => l.LessonId == lessonId);
            if (!lessonExists)
            {
                _logger.LogWarning("Lesson not found with ID: {LessonId}", lessonId);
                return NotFound(new { Message = "Lesson not found." });
            }

            // Lấy danh sách từ vựng với Translations trước khi Select
            var words = await _context.LessonWords
                .Where(lw => lw.LessonId == lessonId)
                .Include(lw => lw.Word)
                .ThenInclude(w => w.Translations)
                .Select(lw => lw.Word)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} words for LessonId: {LessonId}", words.Count, lessonId);
            return Ok(words);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching words for LessonId: {LessonId}", lessonId);
            return StatusCode(500, new { Message = "An unexpected error occurred while fetching words for the lesson." });
        }
    }

    // POST: api/Words
    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<Word>> PostWord([FromBody] WordRequest wordRequest)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received word creation request: {Request}", JsonConvert.SerializeObject(wordRequest));
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
            if (string.IsNullOrWhiteSpace(wordRequest.WordText))
            {
                _logger.LogWarning("WordText is required for word creation");
                return BadRequest(new { Message = "WordText is required." });
            }

            var word = new Word
            {
                WordText = wordRequest.WordText,
                Title = wordRequest.Title,
                Mean = wordRequest.Mean,
                Example = wordRequest.Example,
                Pronunciation = wordRequest.Pronunciation,
                PartOfSpeech = wordRequest.PartOfSpeech,
                AudioUrl = wordRequest.AudioUrl,
                ImageUrl = wordRequest.ImageUrl,
                Level = wordRequest.Level,
                CreatedAt = DateTime.UtcNow,
                Translations = new List<WordTranslation>(),
                LessonWords = new List<LessonWord>(),
                Progresses = new List<UserProgress>(),
                FavoriteWords = new List<FavoriteWord>()
            };

            _context.Words.Add(word);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(word.Title))
            {
                var matchingLessons = await _context.Lessons
                    .Where(l => l.Title == word.Title)
                    .ToListAsync();

                foreach (var lesson in matchingLessons)
                {
                    if (!await _context.LessonWords.AnyAsync(lw => lw.LessonId == lesson.LessonId && lw.WordId == word.WordId))
                    {
                        var lessonWord = new LessonWord
                        {
                            LessonId = lesson.LessonId,
                            WordId = word.WordId
                        };
                        _context.LessonWords.Add(lessonWord);
                        _logger.LogInformation("Added WordId: {WordId} to LessonId: {LessonId} due to matching Title: {Title}",
                            word.WordId, lesson.LessonId, word.Title);
                    }
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Word created successfully by UserId: {UserId}, WordId: {WordId}", userId, word.WordId);
            return CreatedAtAction(nameof(GetWord), new { id = word.WordId }, word);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating word by user with ID: {UserIdClaim}", userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while creating the word." });
        }
    }

    // PUT: api/Words/5
    [HttpPut("{id}")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult> PutWord(int id, [FromBody] WordRequest wordRequest)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received word update request for WordId: {WordId}, Request: {Request}",
                id, JsonConvert.SerializeObject(wordRequest));

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
            if (string.IsNullOrWhiteSpace(wordRequest.WordText))
            {
                _logger.LogWarning("WordText is required for word update");
                return BadRequest(new { Message = "WordText is required." });
            }

            var word = await _context.Words.FirstOrDefaultAsync(w => w.WordId == id);
            if (word == null)
            {
                _logger.LogWarning("Word not found with ID: {WordId}", id);
                return NotFound(new { Message = "Word not found." });
            }

            word.WordText = wordRequest.WordText;
            word.Title = wordRequest.Title;
            word.Mean = wordRequest.Mean;
            word.Example = wordRequest.Example;
            word.Pronunciation = wordRequest.Pronunciation;
            word.PartOfSpeech = wordRequest.PartOfSpeech;
            word.AudioUrl = wordRequest.AudioUrl;
            word.ImageUrl = wordRequest.ImageUrl;
            word.Level = wordRequest.Level;

            if (!string.IsNullOrWhiteSpace(word.Title))
            {
                var matchingLessons = await _context.Lessons
                    .Where(l => l.Title == word.Title)
                    .ToListAsync();

                foreach (var lesson in matchingLessons)
                {
                    if (!await _context.LessonWords.AnyAsync(lw => lw.LessonId == lesson.LessonId && lw.WordId == word.WordId))
                    {
                        var lessonWord = new LessonWord
                        {
                            LessonId = lesson.LessonId,
                            WordId = word.WordId
                        };
                        _context.LessonWords.Add(lessonWord);
                        _logger.LogInformation("Added WordId: {WordId} to LessonId: {LessonId} due to matching Title: {Title}",
                            word.WordId, lesson.LessonId, word.Title);
                    }
                }
            }

            var oldLessonWords = await _context.LessonWords
                .Where(lw => lw.WordId == word.WordId && (word.Title == null || lw.Lesson.Title != word.Title))
                .ToListAsync();
            if (oldLessonWords.Any())
            {
                _context.LessonWords.RemoveRange(oldLessonWords);
                _logger.LogInformation("Removed WordId: {WordId} from {Count} lessons due to Title mismatch",
                    word.WordId, oldLessonWords.Count);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Word updated successfully by UserId: {UserId}, WordId: {WordId}", userId, word.WordId);
            return Ok(new { Message = "Word updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating word with ID: {WordId} by user with ID: {UserIdClaim}", id, userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while updating the word." });
        }
    }

    // GET: api/Words/search
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Word>>> SearchWords([FromQuery] string keyword)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received search request for keyword: {Keyword}", keyword);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                _logger.LogWarning("Keyword is required for search");
                return BadRequest(new { Message = "Keyword is required." });
            }

            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }

            if (!int.TryParse(userIdClaim, out int currentUserId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
            }

            var words = await _context.Words
                .Where(w => w.WordText.Contains(keyword) || (w.Title != null && w.Title.Contains(keyword)))
                .Include(w => w.Translations)
                .Take(20)
                .ToListAsync();

            var search = new DictionarySearch
            {
                UserId = currentUserId,
                Word = keyword,
                SearchTime = DateTime.UtcNow
            };
            _context.DictionarySearches.Add(search);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Search completed for UserId: {UserId}, Keyword: {Keyword}, Found: {Count} words", currentUserId, keyword, words.Count);
            return Ok(words);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching words with keyword: {Keyword} by user with ID: {UserIdClaim}", keyword, userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while searching words." });
        }
    }

    // DELETE: api/Words/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult> DeleteWord(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received word deletion request for WordId: {WordId}", id);

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

            var word = await _context.Words
                .Include(w => w.Translations)
                .Include(w => w.LessonWords)
                .Include(w => w.Progresses)
                .Include(w => w.FavoriteWords)
                .FirstOrDefaultAsync(w => w.WordId == id);

            if (word == null)
            {
                _logger.LogWarning("Word not found with ID: {WordId}", id);
                return NotFound(new { Message = "Word not found." });
            }

            // Xóa các bản ghi liên quan
            _context.WordTranslations.RemoveRange(word.Translations);
            _context.LessonWords.RemoveRange(word.LessonWords);
            _context.UserProgresses.RemoveRange(word.Progresses);
            _context.FavoriteWords.RemoveRange(word.FavoriteWords);

            // Xóa từ vựng
            _context.Words.Remove(word);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Word deleted successfully by UserId: {UserId}, WordId: {WordId}", userId, id);
            return Ok(new { Message = "Word deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting word with ID: {WordId} by user with ID: {UserIdClaim}", id, userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while deleting the word." });
        }
    }

    public class WordRequest
    {
        [Required(ErrorMessage = "WordText is required.")]
        public string WordText { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Mean { get; set; } = string.Empty;
        public string Example { get; set; }
        public string? Pronunciation { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Level { get; set; }
    }
}