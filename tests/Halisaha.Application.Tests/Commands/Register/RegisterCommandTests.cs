using Halisaha.Application.Commands.Register;

namespace Halisaha.Application.Tests.Commands.Register;

public class RegisterCommandTests
{
    [Fact]
    public void Normalize_TrimsTextAndTurnsBlankPhoneIntoNull()
    {
        var command = RegisterCommands.Valid() with
        {
            Email = "  ahmet@example.com ",
            FirstName = " Ahmet ",
            LastName = "\tYılmaz\n",
            PhoneNumber = "   ",
        };

        var normalized = command.Normalize();

        Assert.Equal("ahmet@example.com", normalized.Email);
        Assert.Equal("Ahmet", normalized.FirstName);
        Assert.Equal("Yılmaz", normalized.LastName);
        Assert.Null(normalized.PhoneNumber);
    }

    [Fact]
    public void Normalize_DoesNotTouchPassword()
    {
        // Boşluk parolanın parçası olabilir; kırpmak kullanıcının girdiğinden farklı bir parola kaydederdi.
        var command = RegisterCommands.Valid() with { Password = " halisaha1 " };

        Assert.Equal(" halisaha1 ", command.Normalize().Password);
    }

    [Fact]
    public void Normalize_TurnsMissingTextIntoEmpty()
    {
        // JSON'da gelmeyen alan runtime'da null olur (nullable annotation zorlamaz). Boş string'e çevrilir ki
        // validator "boş olamaz" desin, handler NullReferenceException görmesin.
        var command = RegisterCommands.Valid() with { Email = null!, Password = null!, FirstName = null!, LastName = null! };

        var normalized = command.Normalize();

        Assert.Equal(string.Empty, normalized.Email);
        Assert.Equal(string.Empty, normalized.Password);
        Assert.Equal(string.Empty, normalized.FirstName);
        Assert.Equal(string.Empty, normalized.LastName);
    }

    [Fact]
    public void ToString_DoesNotContainThePassword()
    {
        var command = RegisterCommands.Valid() with { Password = "cok-gizli-parola-42" };

        var text = command.ToString();

        Assert.DoesNotContain("cok-gizli-parola-42", text);
        Assert.Contains("Password = ***", text);
        Assert.Contains(command.Email, text); // geri kalan alanlar hâlâ görünür (teşhis için)
    }
}
