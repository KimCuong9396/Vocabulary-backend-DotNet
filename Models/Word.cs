namespace VocabularyApp.Models;

public class Word
{
    public int WordId { get; set; }
    public string WordText { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Level { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WordTranslation> Translations { get; set; } = new();
    public List<LessonWord> LessonWords { get; set; } = new();
    public List<UserProgress> Progresses { get; set; } = new();
    public List<FavoriteWord> FavoriteWords { get; set; } = new();
}