using System.ComponentModel.DataAnnotations;

namespace FinSight.Api.Website.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required, Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;
    [Required]
    public string Location { get; set; } = string.Empty;
    [Required]
    public string Currency { get; set; } = "IQD";
    [Required, Display(Name = "Business contact information")]
    public string ContactInformation { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ResendConfirmationViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
