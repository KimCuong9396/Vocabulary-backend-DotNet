namespace VocabularyApp.Models;

public class FavoriteWord
{
    public int FavoriteId { get; set; }
    public int UserId { get; set; }
    public int WordId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Word Word { get; set; } = null!;
}