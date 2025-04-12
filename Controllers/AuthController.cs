using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using VocabularyApp.Services;

namespace VocabularyApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for register request: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            await _authService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully with username: {Username}", registerDto.Username);

            // Lấy thông tin user để trả về
            var user = await _authService.GetUserByUsernameAsync(registerDto.Username);
            if (user == null)
            {
                _logger.LogWarning("User not found after registration: {Username}", registerDto.Username);
                return StatusCode(500, new { Message = "User registration succeeded but user not found." });
            }

            return Ok(new
            {
                user.UserId,
                user.Username,
                user.Email
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for username {Username}: {Message}", registerDto.Username, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for username: {Username}", registerDto.Username);
            return StatusCode(500, new { Message = "An unexpected error occurred during registration." });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for login request: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            var token = await _authService.LoginAsync(loginDto);
            _logger.LogInformation("User logged in successfully: {Username}", loginDto.Username);
            return Ok(token);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for username {Username}: {Message}", loginDto.Username, ex.Message);
            return Unauthorized(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username: {Username}", loginDto.Username);
            return StatusCode(500, new { Message = "An unexpected error occurred during login." });
        }
    }
}