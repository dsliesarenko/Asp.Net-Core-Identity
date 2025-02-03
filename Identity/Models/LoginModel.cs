using System.ComponentModel.DataAnnotations;

namespace Identity.Models
{
    public class LoginModel
    {
        [Required]
        public string Email { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
        [Required]
        public bool RememberMe { get; set; } = false;
    }
}
