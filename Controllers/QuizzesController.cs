using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuizzesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Quizzes/lesson/5
    [HttpGet("lesson/{lessonId}")]
    public async Task<ActionResult<IEnumerable<Quiz>>> GetQuizzesByLesson(int lessonId)
    {
        var quizzes = await _context.Quizzes
            .Where(q => q.LessonId == lessonId)
            .ToListAsync();

        return Ok(quizzes);
    }

    // GET: api/Quizzes/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Quiz>> GetQuiz(int id)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
            .FirstOrDefaultAsync(q => q.QuizId == id);

        if (quiz == null)
        {
            return NotFound(new { message = "Quiz not found." });
        }

        return Ok(quiz);
    }

    // POST: api/Quizzes
    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<Quiz>> CreateQuiz(Quiz quiz)
    {
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetQuiz), new { id = quiz.QuizId }, quiz);
    }
}