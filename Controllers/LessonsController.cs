using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LessonsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<LessonsController> _logger;


    public LessonsController(AppDbContext context,ILogger<LessonsController> logger )
    {
        _context = context;
        _logger = logger;

    }

    // GET: api/Lessons/course/5
    [HttpGet("course/{courseId}")]
    public async Task<ActionResult<IEnumerable<Lesson>>> GetLessonsByCourse(int courseId)
    {
        var lessons = await _context.Lessons
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.OrderInCourse)
            .ToListAsync();

        return Ok(lessons);
    }

    // GET: api/Lessons/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Lesson>> GetLesson(int id)
    {
        var lesson = await _context.Lessons
            .Include(l => l.LessonWords)
            .ThenInclude(lw => lw.Word)
            .ThenInclude(w => w.Translations)
            .FirstOrDefaultAsync(l => l.LessonId == id);

        if (lesson == null)
        {
            return NotFound(new { message = "Lesson not found." });
        }

        return Ok(lesson);
    }

    // POST: api/Lessons
    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<Lesson>> CreateLesson([FromBody] LessonRequest lessonRequest)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;

        try
        {
            _logger.LogInformation("Received lesson creation request: {Request}", JsonConvert.SerializeObject(lessonRequest));
            
            // Kiểm tra userIdClaim
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }
            
            // Kiểm tra và parse userId
            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
            }
            
            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(lessonRequest.Title))
            {
                _logger.LogWarning("Title is required for lesson creation");
                return BadRequest(new { Message = "Title is required." });
            }

            if (lessonRequest.CourseId <= 0)
            {
                _logger.LogWarning("Invalid CourseId: {CourseId}", lessonRequest.CourseId);
                return BadRequest(new { Message = "CourseId must be a positive integer." });
            }

            if (lessonRequest.OrderInCourse < 0)
            {
                _logger.LogWarning("Invalid OrderInCourse: {OrderInCourse}", lessonRequest.OrderInCourse);
                return BadRequest(new { Message = "OrderInCourse cannot be negative." });
            }
            
            // Tạo mới lesson
            var lesson = new Lesson
            {
                CourseId = lessonRequest.CourseId,
                Title = lessonRequest.Title,
                Description = lessonRequest.Description,
                ImageUrl = lessonRequest.ImageUrl,
                OrderInCourse = lessonRequest.OrderInCourse,
                LessonWords = new List<LessonWord>(),
                Quizzes = new List<Quiz>()
            };
            
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Lesson created successfully by UserId: {UserId}, LessonId: {LessonId}", userId, lesson.LessonId);
            return CreatedAtAction(nameof(GetLesson), new { id = lesson.LessonId }, lesson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lesson by user with ID: {UserIdClaim}", userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while creating the lesson." });
        }
    }

    // DELETE: api/Lessons/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Premium")]
    public async Task<IActionResult> DeleteLesson(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.FindFirst("sub")?.Value;
        
        try
        {
            _logger.LogInformation("Received lesson deletion request for LessonId: {LessonId}", id);
            
            // Kiểm tra userIdClaim
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }
            
            // Kiểm tra và parse userId
            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
            }
            
            // Tìm lesson cần xóa
            var lesson = await _context.Lessons
                .Include(l => l.LessonWords)
                .Include(l => l.Quizzes)
                .FirstOrDefaultAsync(l => l.LessonId == id);
                
            if (lesson == null)
            {
                _logger.LogWarning("Lesson not found with ID: {LessonId}", id);
                return NotFound(new { Message = "Lesson not found." });
            }
            
            // Xóa lesson và các entities liên quan
            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Lesson deleted successfully by UserId: {UserId}, LessonId: {LessonId}", userId, id);
            return Ok(new { Message = "Lesson deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lesson with ID: {LessonId} by user with ID: {UserIdClaim}", id, userIdClaim ?? "unknown");
            return StatusCode(500, new { Message = "An unexpected error occurred while deleting the lesson." });
        }
    }

    // PUT: api/Lessons/5
[HttpPut("{id}")]
[Authorize(Roles = "Premium")]
public async Task<ActionResult> UpdateLesson(int id, [FromBody] LessonRequest lessonRequest)
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst("sub")?.Value;

    try
    {
        _logger.LogInformation("Received lesson update request for LessonId: {LessonId}, Request: {Request}",
            id, JsonConvert.SerializeObject(lessonRequest));

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
        if (string.IsNullOrWhiteSpace(lessonRequest.Title))
        {
            _logger.LogWarning("Title is required for lesson update");
            return BadRequest(new { Message = "Title is required." });
        }
        if (lessonRequest.CourseId <= 0)
        {
            _logger.LogWarning("Invalid CourseId: {CourseId}", lessonRequest.CourseId);
            return BadRequest(new { Message = "CourseId must be a positive integer." });
        }
        if (lessonRequest.OrderInCourse < 0)
        {
            _logger.LogWarning("Invalid OrderInCourse: {OrderInCourse}", lessonRequest.OrderInCourse);
            return BadRequest(new { Message = "OrderInCourse cannot be negative." });
        }

        var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.LessonId == id);
        if (lesson == null)
        {
            _logger.LogWarning("Lesson not found with ID: {LessonId}", id);
            return NotFound(new { Message = "Lesson not found." });
        }

        lesson.CourseId = lessonRequest.CourseId;
        lesson.Title = lessonRequest.Title;
        lesson.Description = lessonRequest.Description;
        lesson.ImageUrl = lessonRequest.ImageUrl;
        lesson.OrderInCourse = lessonRequest.OrderInCourse;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Lesson updated successfully by UserId: {UserId}, LessonId: {LessonId}", userId, lesson.LessonId);
        return Ok(new { Message = "Lesson updated successfully." });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating lesson with ID: {LessonId} by user with ID: {UserIdClaim}", id, userIdClaim ?? "unknown");
        return StatusCode(500, new { Message = "An unexpected error occurred while updating the lesson." });
    }
}

// GET: api/Lessons
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Lesson>>> GetAllLessons()
    {
        try
        {
            _logger.LogInformation("Đang lấy tất cả bài học");
            var lessons = await _context.Lessons
                .OrderBy(l => l.CourseId)
                .ThenBy(l => l.OrderInCourse)
                .ToListAsync();
            _logger.LogInformation("Đã lấy {Count} bài học", lessons.Count);
            return Ok(lessons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy tất cả bài học");
            return StatusCode(500, new { Message = "Đã xảy ra lỗi khi lấy danh sách bài học." });
        }
    }
public class LessonRequest
{
    public int CourseId { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int OrderInCourse { get; set; }
}
}