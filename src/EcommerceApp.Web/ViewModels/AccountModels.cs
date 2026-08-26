using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Web.ViewModels;

public sealed class RegisterModel { [Required, StringLength(80)] public string DisplayName { get; set; } = ""; [Required, EmailAddress] public string Email { get; set; } = ""; [Required, DataType(DataType.Password)] public string Password { get; set; } = ""; [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = ""; public string? ReturnUrl { get; set; } }
public sealed class LoginModel { [Required, EmailAddress] public string Email { get; set; } = ""; [Required, DataType(DataType.Password)] public string Password { get; set; } = ""; public bool RememberMe { get; set; } public string? ReturnUrl { get; set; } }
