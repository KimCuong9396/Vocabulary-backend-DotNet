using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CoursesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Courses
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Course>>> GetCourses()
    {
        return await _context.Courses.ToListAsync();
    }

    // GET: api/Courses/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Course>> GetCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Lessons)
            .FirstOrDefaultAsync(c => c.CourseId == id);

        if (course == null)
        {
            return NotFound(new { message = "Course not found." });
        }

        return Ok(course);
    }

    // POST: api/Courses
    [HttpPost]
    [Authorize(Roles = "Premium")] // Chỉ Premium được tạo khóa học
    public async Task<ActionResult<Course>> CreateCourse(Course course)
    {
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCourse), new { id = course.CourseId }, course);
    }

    // PUT: api/Courses/5
    [HttpPut("{id}")]
    [Authorize(Roles = "Premium")]
    public async Task<IActionResult> UpdateCourse(int id, Course course)
    {
        if (id != course.CourseId)
        {
            return BadRequest(new { message = "Course ID mismatch." });
        }

        _context.Entry(course).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CourseExists(id))
            {
                return NotFound(new { message = "Course not found." });
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // DELETE: api/Courses/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Premium")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Lessons)
            .ThenInclude(l => l.LessonWords)
            .Include(c => c.Lessons)
            .ThenInclude(l => l.Quizzes)
            .FirstOrDefaultAsync(c => c.CourseId == id);
            
        if (course == null)
        {
            return NotFound(new { message = "Course not found." });
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Course deleted successfully." });
    }

    private bool CourseExists(int id)
    {
        return _context.Courses.Any(e => e.CourseId == id);
    }
}