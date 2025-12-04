using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı gerekli.")]
    [Display(Name = "Kullanıcı Adı")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
