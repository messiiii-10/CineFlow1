using System.ComponentModel.DataAnnotations;

namespace CineFlow.Models
{
    public class Kullanici
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [StringLength(40, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3 ile 40 karakter arasında olmalıdır.")]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        public string Password { get; set; } = string.Empty;

        [StringLength(260)]
        public string? ProfilResmiYolu { get; set; }

        [StringLength(280)]
        public string? Biyografi { get; set; }
    }
}

