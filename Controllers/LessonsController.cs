using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LessonsController : ControllerBase
{
    private readonly AppDbContext _context;

    public LessonsController(AppDbContext context)
    {
        _context = context;
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
    public async Task<ActionResult<Lesson>> CreateLesson(Lesson lesson)
    {
        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLesson), new { id = lesson.LessonId }, lesson);
    }
}