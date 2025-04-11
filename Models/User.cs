namespace VocabularyApp.Models;

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Mật khẩu đã mã hóa
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool IsPremium { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UserProgress> Progresses { get; set; } = new();
    public List<FavoriteWord> FavoriteWords { get; set; } = new();
    public List<QuizResult> QuizResults { get; set; } = new();
     public List<DictionarySearch> DictionarySearches { get; set; }= null!;
}