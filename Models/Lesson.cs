namespace VocabularyApp.Models;

public class Lesson
{
    public int LessonId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderInCourse { get; set; }

    public Course Course { get; set; }
    public List<LessonWord> LessonWords { get; set; } = new();
    public List<Quiz> Quizzes { get; set; } = new();
}