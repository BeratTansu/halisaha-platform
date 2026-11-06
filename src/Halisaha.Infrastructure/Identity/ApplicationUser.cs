using Microsoft.AspNetCore.Identity;

namespace Halisaha.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    // required: set edilmeyi unutmak derleme hatası olur; aksi halde 0001-01-01 sessizce kaydedilirdi.
    // Değeri Register handler'ı TimeProvider ile verir. Şema değişikliği değildir (kolon zaten NOT NULL).
    public required DateTimeOffset CreatedAt { get; set; }
}
