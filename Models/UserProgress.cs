namespace VocabularyApp.Models;

public class UserProgress
{
    public int ProgressId { get; set; }
    public int UserId { get; set; }
    public int WordId { get; set; }
    public int MemoryLevel { get; set; } = 1;
    public DateTime? LastReviewed { get; set; }
    public DateTime? NextReview { get; set; }
    public int ReviewCount { get; set; } = 0;
    public string Status { get; set; } = "NotLearned";

    public User User { get; set; } = null!;
    public Word Word { get; set; } = null!;
}