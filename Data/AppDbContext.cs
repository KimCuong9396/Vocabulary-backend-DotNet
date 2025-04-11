using Microsoft.EntityFrameworkCore;
using VocabularyApp.Models;

namespace VocabularyApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<Word> Words { get; set; }
    public DbSet<WordTranslation> WordTranslations { get; set; }
    public DbSet<LessonWord> LessonWords { get; set; }
    public DbSet<UserProgress> UserProgresses { get; set; }
    public DbSet<FavoriteWord> FavoriteWords { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<QuizResult> QuizResults { get; set; }
    public DbSet<DictionarySearch> DictionarySearches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Định nghĩa khóa chính và mối quan hệ
        modelBuilder.Entity<User>().HasKey(u => u.UserId);
        modelBuilder.Entity<Course>().HasKey(c => c.CourseId);
        modelBuilder.Entity<Lesson>().HasKey(l => l.LessonId);
        modelBuilder.Entity<Word>().HasKey(w => w.WordId);
        modelBuilder.Entity<WordTranslation>().HasKey(wt => wt.TranslationId);
        modelBuilder.Entity<LessonWord>().HasKey(lw => lw.LessonWordId);
        modelBuilder.Entity<UserProgress>().HasKey(up => up.ProgressId);
        modelBuilder.Entity<FavoriteWord>().HasKey(fw => fw.FavoriteId);
        modelBuilder.Entity<Quiz>().HasKey(q => q.QuizId);
        modelBuilder.Entity<QuizResult>().HasKey(qr => qr.ResultId);
        modelBuilder.Entity<DictionarySearch>().HasKey(d => d.SearchId);


        // Định nghĩa các mối quan hệ
        modelBuilder.Entity<Lesson>()
            .HasOne(l => l.Course)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CourseId);

        modelBuilder.Entity<WordTranslation>()
            .HasOne(wt => wt.Word)
            .WithMany(w => w.Translations)
            .HasForeignKey(wt => wt.WordId);

        modelBuilder.Entity<LessonWord>()
            .HasOne(lw => lw.Lesson)
            .WithMany(l => l.LessonWords)
            .HasForeignKey(lw => lw.LessonId);

        modelBuilder.Entity<LessonWord>()
            .HasOne(lw => lw.Word)
            .WithMany(w => w.LessonWords)
            .HasForeignKey(lw => lw.WordId);

        modelBuilder.Entity<UserProgress>()
            .HasOne(up => up.User)
            .WithMany(u => u.Progresses)
            .HasForeignKey(up => up.UserId);

        modelBuilder.Entity<UserProgress>()
            .HasOne(up => up.Word)
            .WithMany(w => w.Progresses)
            .HasForeignKey(up => up.WordId);

        modelBuilder.Entity<FavoriteWord>()
            .HasOne(fw => fw.User)
            .WithMany(u => u.FavoriteWords)
            .HasForeignKey(fw => fw.UserId);

        modelBuilder.Entity<FavoriteWord>()
            .HasOne(fw => fw.Word)
            .WithMany(w => w.FavoriteWords)
            .HasForeignKey(fw => fw.WordId);

        modelBuilder.Entity<Quiz>()
            .HasOne(q => q.Lesson)
            .WithMany(l => l.Quizzes)
            .HasForeignKey(q => q.LessonId);

        modelBuilder.Entity<QuizResult>()
            .HasOne(qr => qr.User)
            .WithMany(u => u.QuizResults)
            .HasForeignKey(qr => qr.UserId);

        modelBuilder.Entity<QuizResult>()
            .HasOne(qr => qr.Quiz)
            .WithMany(q => q.Results)
            .HasForeignKey(qr => qr.QuizId);

        // Đảm bảo Username và Email là duy nhất
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<DictionarySearch>()
            .HasOne(ds => ds.User)
            .WithMany(u => u.DictionarySearches)
            .HasForeignKey(ds => ds.UserId);
    }
}