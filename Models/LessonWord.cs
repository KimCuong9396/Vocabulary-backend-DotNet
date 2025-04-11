namespace VocabularyApp.Models;

public class LessonWord
{
    public int LessonWordId { get; set; }
    public int LessonId { get; set; }
    public int WordId { get; set; }

    public Lesson Lesson { get; set; } = null!;
    public Word Word { get; set; } = null!;
}