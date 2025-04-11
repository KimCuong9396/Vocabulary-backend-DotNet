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
public class WordsController : ControllerBase
{
    private readonly AppDbContext _context;

    public WordsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Words
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Word>>> GetWords()
    {
        return await _context.Words.ToListAsync();
    }

    // GET: api/Words/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Word>> GetWord(int id)
    {
        var word = await _context.Words
            .Include(w => w.Translations)
            .FirstOrDefaultAsync(w => w.WordId == id);

        if (word == null)
        {
            return NotFound(new { message = "Word not found." });
        }

        return Ok(word);
    }

    // POST: api/Words
    [HttpPost]
    [Authorize(Roles = "Premium")]
    public async Task<ActionResult<Word>> PostWord(Word word)
    {
        _context.Words.Add(word);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWord), new { id = word.WordId }, word);
    }

    // GET: api/Words/search?keyword=app
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Word>>> SearchWords([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return BadRequest(new { message = "Keyword is required." });
        }

        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Tìm kiếm từ vựng (khớp gần đúng)
        var words = await _context.Words
            .Where(w => w.WordText.Contains(keyword))
            .Include(w => w.Translations)
            .Take(20) // Giới hạn 20 kết quả
            .ToListAsync();

        // Lưu lịch sử tra cứu
        var search = new DictionarySearch
        {
            UserId = currentUserId,
            Word = keyword
        };
        _context.DictionarySearches.Add(search);
        await _context.SaveChangesAsync();

        return Ok(words);
    }
}