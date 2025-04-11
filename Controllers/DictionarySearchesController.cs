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
public class DictionarySearchesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DictionarySearchesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/DictionarySearches
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DictionarySearch>>> GetSearchHistory()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var searches = await _context.DictionarySearches
            .Where(ds => ds.UserId == currentUserId)
            .OrderByDescending(ds => ds.SearchTime)
            .Take(50) // Giới hạn 50 bản ghi gần nhất
            .ToListAsync();

        return Ok(searches);
    }

    // POST: api/DictionarySearches
    [HttpPost]
    public async Task<ActionResult<DictionarySearch>> AddSearch([FromBody] AddSearchDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var search = new DictionarySearch
        {
            UserId = currentUserId,
            Word = dto.Word
        };

        _context.DictionarySearches.Add(search);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSearchHistory), new { id = search.SearchId }, search);
    }
}

// DTO để thêm lịch sử tra cứu
public class AddSearchDto
{
    public string Word { get; set; } = string.Empty;
}