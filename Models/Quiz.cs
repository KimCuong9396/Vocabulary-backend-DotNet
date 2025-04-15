namespace VocabularyApp.Models;

public class Quiz
{
    public int QuizId { get; set; }
    public int LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? QuizType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Lesson Lesson { get; set; }
    public List<QuizResult> Results { get; set; } = new();
}