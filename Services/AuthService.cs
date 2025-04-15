using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using VocabularyApp.Data;
using VocabularyApp.Models;

namespace VocabularyApp.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenDto> LoginAsync(LoginDto loginDto)
    {
        _logger.LogInformation("Attempting login for username: {Username}", loginDto.Username);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid login attempt for username: {Username}", loginDto.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        _logger.LogInformation("User authenticated: {UserId}, Username: {Username}", user.UserId, user.Username);

        var token = GenerateJwtToken(user);
        return new TokenDto
        {
            Token = token,
            Expiry = DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60"))
        };
    }

    public async Task RegisterAsync(RegisterDto registerDto)
    {
        _logger.LogInformation("Attempting to register user with username: {Username}", registerDto.Username);

        var existingUserByUsername = await _context.Users
            .AnyAsync(u => u.Username == registerDto.Username);
        if (existingUserByUsername)
        {
            _logger.LogWarning("Registration failed: Username {Username} already exists.", registerDto.Username);
            throw new InvalidOperationException("Username is already taken.");
        }

        var existingUserByEmail = await _context.Users
            .AnyAsync(u => u.Email == registerDto.Email);
        if (existingUserByEmail)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists.", registerDto.Email);
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            Username = registerDto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            Email = registerDto.Email,
            FullName = registerDto.FullName,
            PreferredLanguage = registerDto.PreferredLanguage ?? "en",
            IsPremium = false
        };

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User registered successfully: {UserId}, Username: {Username}", user.UserId, user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with username: {Username}", registerDto.Username);
            throw new InvalidOperationException("An error occurred while registering the user.");
        }
    }

    public async Task RegisterAdminAsync(RegisterDto registerDto)
    {
        _logger.LogInformation("Attempting to register admin with username: {Username}", registerDto.Username);

        var existingUserByUsername = await _context.Users
            .AnyAsync(u => u.Username == registerDto.Username);
        if (existingUserByUsername)
        {
            _logger.LogWarning("Admin registration failed: Username {Username} already exists.", registerDto.Username);
            throw new InvalidOperationException("Username is already taken.");
        }

        var existingUserByEmail = await _context.Users
            .AnyAsync(u => u.Email == registerDto.Email);
        if (existingUserByEmail)
        {
            _logger.LogWarning("Admin registration failed: Email {Email} already exists.", registerDto.Email);
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            Username = registerDto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            Email = registerDto.Email,
            FullName = registerDto.FullName,
            PreferredLanguage = registerDto.PreferredLanguage ?? "en",
            IsPremium = true // Admin có role Premium
        };

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin registered successfully: {UserId}, Username: {Username}", user.UserId, user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering admin with username: {Username}", registerDto.Username);
            throw new InvalidOperationException("An error occurred while registering the admin.");
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        _logger.LogDebug("Fetching user with username: {Username}", username);
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            _logger.LogWarning("User not found with username: {Username}", username);
        }
        return user;
    }

    private string GenerateJwtToken(User user)
    {
        var keyValue = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing");
        _logger.LogDebug("Generating token with key length: {KeyLength}", keyValue.Length);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(ClaimTypes.Role, user.IsPremium ? "Premium" : "Free"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60")),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogDebug("Token generated for user: {UserId}", user.UserId);
        return tokenString;
    }
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
}

public class TokenDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}