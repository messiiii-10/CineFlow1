using System.ComponentModel.DataAnnotations;

namespace CineFlow.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Display(Name = "E-posta")]
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        public string Email { get; set; } = string.Empty;
    }
}
