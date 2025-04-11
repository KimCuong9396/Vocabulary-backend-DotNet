namespace VocabularyApp.Models;

public class WordTranslation
{
    public int TranslationId { get; set; }
    public int WordId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? ExampleSentence { get; set; }

    public Word Word { get; set; } = null!;
}