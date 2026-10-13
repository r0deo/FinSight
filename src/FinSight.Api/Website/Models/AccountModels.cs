using System.ComponentModel.DataAnnotations;

namespace FinSight.Api.Website.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "{0} is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Location")]
    public string Location { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Currency")]
    public string Currency { get; set; } = "IQD";
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Business contact information")]
    public string ContactInformation { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), DataType(DataType.Password),
        Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "{0} is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
