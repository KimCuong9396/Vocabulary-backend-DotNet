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
public class FavoriteWordsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FavoriteWordsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/FavoriteWords
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FavoriteWord>>> GetFavoriteWords()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var favorites = await _context.FavoriteWords
            .Where(fw => fw.UserId == currentUserId)
            .Include(fw => fw.Word)
            .ThenInclude(w => w.Translations)
            .ToListAsync();

        return Ok(favorites);
    }

    // POST: api/FavoriteWords
    [HttpPost]
    public async Task<ActionResult<FavoriteWord>> AddFavoriteWord([FromBody] AddFavoriteDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var word = await _context.Words.FindAsync(dto.WordId);
        if (word == null)
        {
            return NotFound(new { message = "Word not found." });
        }

        var existingFavorite = await _context.FavoriteWords
            .FirstOrDefaultAsync(fw => fw.UserId == currentUserId && fw.WordId == dto.WordId);
        if (existingFavorite != null)
        {
            return BadRequest(new { message = "Word already in favorites." });
        }

        var favorite = new FavoriteWord
        {
            UserId = currentUserId,
            WordId = dto.WordId
        };

        _context.FavoriteWords.Add(favorite);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFavoriteWords), new { id = favorite.FavoriteId }, favorite);
    }

    // DELETE: api/FavoriteWords/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFavoriteWord(int id)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var favorite = await _context.FavoriteWords.FindAsync(id);
        if (favorite == null || favorite.UserId != currentUserId)
        {
            return NotFound(new { message = "Favorite word not found or not owned by user." });
        }

        _context.FavoriteWords.Remove(favorite);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Favorite word removed." });
    }
}

// DTO để thêm từ yêu thích
public class AddFavoriteDto
{
    public int WordId { get; set; }
}