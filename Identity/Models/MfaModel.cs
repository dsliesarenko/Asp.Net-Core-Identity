using System.ComponentModel.DataAnnotations;

namespace Identity.Models
{
    public class MfaModel
    {
        [Required]
        public string SecurityCode { get; set; } = null!;

        public bool RememberMe { get; set; }
    }
}
