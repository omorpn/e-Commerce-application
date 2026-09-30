using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    // Small key/value store for app state such as the sample catalog version.
    public class SiteSetting
    {
        [Key, StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Value { get; set; }
    }
}
