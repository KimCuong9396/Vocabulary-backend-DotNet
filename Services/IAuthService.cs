using VocabularyApp.DTOs;
using VocabularyApp.Models;

namespace VocabularyApp.Services;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterDto registerDto);
    Task<TokenDto> LoginAsync(LoginDto loginDto);
}