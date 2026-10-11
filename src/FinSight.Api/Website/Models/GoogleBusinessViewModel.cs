using System.ComponentModel.DataAnnotations;

namespace FinSight.Api.Website.Models;

public sealed class GoogleBusinessViewModel
{
    [Required, Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;
    [Required]
    public string Location { get; set; } = string.Empty;
    [Required]
    public string Currency { get; set; } = "IQD";
    [Required, Display(Name = "Business contact information")]
    public string ContactInformation { get; set; } = string.Empty;
}
