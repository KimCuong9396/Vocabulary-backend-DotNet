using System.ComponentModel.DataAnnotations;

namespace VocabularyApp.Models.Dtos;

public class UserProgressRequest
{
    [Required(ErrorMessage = "WordId is required.")]
    public int WordId { get; set; }

    public int MemoryLevel { get; set; }

    public string? LastReviewed { get; set; }

    public string? NextReview { get; set; }

    public int ReviewCount { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = "NotLearned";
}