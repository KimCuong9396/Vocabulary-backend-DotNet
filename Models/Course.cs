namespace VocabularyApp.Models;

public class Course
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Level { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Lesson> Lessons { get; set; } = new();
}