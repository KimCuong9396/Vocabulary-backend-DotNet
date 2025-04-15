namespace VocabularyApp.Models.Dtos;

public class UserProgressDto
{
    public int ProgressId { get; set; }
    public int UserId { get; set; }
    public int WordId { get; set; }
    public int MemoryLevel { get; set; }
    public DateTime? LastReviewed { get; set; }
    public DateTime? NextReview { get; set; }
    public int ReviewCount { get; set; }
    public string Status { get; set; } = "NotLearned";
    public WordDto Word { get; set; } = null!;
}

public class WordDto
{
    public int WordId { get; set; }
    public string WordText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public List<TranslationDto> Translations { get; set; } = new();
}

public class TranslationDto
{
    public int TranslationId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
}