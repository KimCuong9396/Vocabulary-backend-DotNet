using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Newtonsoft.Json;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DictionarySearchesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<DictionarySearchesController> _logger;

    public DictionarySearchesController(AppDbContext context, ILogger<DictionarySearchesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/DictionarySearches
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DictionarySearch>>> GetSearchHistory()
    {
        try
        {
            // Log Authorization header
            var authHeader = Request.Headers["Authorization"].ToString();
            _logger.LogDebug("Authorization header: {AuthHeader}", authHeader.Length > 50 ? "[Truncated]" : authHeader);

            // Log tất cả claims
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            _logger.LogInformation("User claims: {Claims}", claims.Any() ? JsonConvert.SerializeObject(claims, Formatting.Indented) : "None");

            // Thử truy cập user ID từ nameidentifier hoặc sub
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Missing or empty user ID claim in JWT token. Claims available: {Claims}", claims.Any() ? JsonConvert.SerializeObject(claims, Formatting.Indented) : "None");
                return Unauthorized(new { Message = "Invalid token: Missing user ID." });
            }

            if (!int.TryParse(userIdClaim, out int currentUserId))
            {
                _logger.LogWarning("Invalid user ID claim value: {UserIdClaim}. Cannot parse to integer.", userIdClaim);
                return Unauthorized(new { Message = "Invalid token: User ID is not valid." });
            }

            var searches = await _context.DictionarySearches
                .Where(ds => ds.UserId == currentUserId)
                .OrderByDescending(ds => ds.SearchTime)
                .Take(50) // Giới hạn 50 bản ghi gần nhất
                .ToListAsync();

            _logger.LogInformation("Retrieved search history for UserId: {UserId}. Total records: {RecordCount}", currentUserId, searches.Count);
            return Ok(searches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search history for user with user ID claim: {UserIdClaim}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { Message = "An unexpected error occurred while retrieving search history." });
        }
    }

    // POST: api/DictionarySearches
    [HttpPost]
    public async Task<ActionResult<DictionarySearch>> AddSearch([FromBody] AddSearchDto dto)
    {
        try
        {
            // Log thông tin request
            _logger.LogInformation("Received add search request: {Request}", JsonConvert.SerializeObject(dto));

            // Thử truy cập user ID từ nameidentifier hoặc sub
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

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

            // Kiểm tra DTO
            if (string.IsNullOrWhiteSpace(dto.Word))
            {
                _logger.LogWarning("Invalid search word provided by UserId: {UserId}", currentUserId);
                return BadRequest(new { Message = "Search word cannot be empty." });
            }

            var search = new DictionarySearch
            {
                UserId = currentUserId,
                Word = dto.Word,
                SearchTime = DateTime.UtcNow // Thêm thời gian tìm kiếm
            };

            _context.DictionarySearches.Add(search);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Search added successfully for UserId: {UserId}, Word: {Word}", currentUserId, dto.Word);
            return CreatedAtAction(nameof(GetSearchHistory), new { id = search.SearchId }, search);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding search for user with user ID claim: {UserIdClaim}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { Message = "An unexpected error occurred while adding the search." });
        }
    }

    // DELETE: api/DictionarySearches/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSearch(int id)
    {
        try
        {
            // Log thông tin request
            _logger.LogInformation("Received delete search request for SearchId: {SearchId}", id);

            // Thử truy cập user ID từ nameidentifier hoặc sub
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

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

            // Tìm mục lịch sử tìm kiếm
            var search = await _context.DictionarySearches
                .Where(ds => ds.SearchId == id && ds.UserId == currentUserId)
                .FirstOrDefaultAsync();

            if (search == null)
            {
                _logger.LogWarning("Search with SearchId: {SearchId} not found or does not belong to UserId: {UserId}", id, currentUserId);
                return NotFound(new { Message = "Search history entry not found or you do not have permission to delete it." });
            }

            _context.DictionarySearches.Remove(search);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Search deleted successfully for SearchId: {SearchId}, UserId: {UserId}", id, currentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting search for SearchId: {SearchId}, user with user ID claim: {UserIdClaim}", id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { Message = "An unexpected error occurred while deleting the search." });
        }
    }

    // DELETE: api/DictionarySearches
    [HttpDelete]
    public async Task<IActionResult> DeleteAllSearches()
    {
        try
        {
            // Log thông tin request
            _logger.LogInformation("Received request to delete all search history");

            // Thử truy cập user ID từ nameidentifier hoặc sub
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
            _logger.LogDebug("Extracted user ID claim: {UserIdClaim}", userIdClaim ?? "null");

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

            // Tìm tất cả lịch sử tìm kiếm của người dùng
            var searches = await _context.DictionarySearches
                .Where(ds => ds.UserId == currentUserId)
                .ToListAsync();

            if (!searches.Any())
            {
                _logger.LogInformation("No search history found for UserId: {UserId}", currentUserId);
                return NoContent();
            }

            // Xóa tất cả các bản ghi
            _context.DictionarySearches.RemoveRange(searches);
            await _context.SaveChangesAsync();

            _logger.LogInformation("All search history deleted successfully for UserId: {UserId}. Total records deleted: {RecordCount}", currentUserId, searches.Count);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all search history for user with user ID claim: {UserIdClaim}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { Message = "An unexpected error occurred while deleting all search history." });
        }
    }
}

// DTO để thêm lịch sử tra cứu
public class AddSearchDto
{
    public string Word { get; set; } = string.Empty;
}