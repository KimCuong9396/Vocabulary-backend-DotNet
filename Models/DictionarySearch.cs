namespace VocabularyApp.Models;

public class DictionarySearch
{
    public int SearchId { get; set; }
    public int UserId { get; set; }
    public string Word { get; set; } = string.Empty;
    public DateTime SearchTime { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}