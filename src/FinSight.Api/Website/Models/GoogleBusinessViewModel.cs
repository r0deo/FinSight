using System.ComponentModel.DataAnnotations;

namespace FinSight.Api.Website.Models;

public sealed class GoogleBusinessViewModel
{
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Location")]
    public string Location { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Currency")]
    public string Currency { get; set; } = "IQD";
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Business contact information")]
    public string ContactInformation { get; set; } = string.Empty;
}
