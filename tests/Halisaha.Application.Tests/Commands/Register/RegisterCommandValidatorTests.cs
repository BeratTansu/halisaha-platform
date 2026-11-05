using Halisaha.Application.Commands.Register;
using Halisaha.Domain.Enums;

namespace Halisaha.Application.Tests.Commands.Register;

/// <summary>
/// Validator yalnızca isteğin şeklini kontrol eder. Testlerin bir kısmı bilerek "burada hata YOK" der:
/// parola politikası Identity'de, seviye/pozisyon domain'de; validator onları tekrar etmemeli.
/// </summary>
public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        Assert.True(_validator.Validate(RegisterCommands.Valid()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void InvalidEmail_IsRejectedOnEmail(string email)
    {
        AssertSingleErrorOn(nameof(RegisterCommand.Email), RegisterCommands.Valid() with { Email = email });
    }

    [Fact]
    public void EmailLongerThanColumn_IsRejected()
    {
        // Identity şemasında Email varchar(256); daha uzunu DB'de 22001 (500) verirdi.
        var email = new string('a', RegisterCommandValidator.MaxEmailLength - "@x.co".Length + 1) + "@x.co";

        AssertHasErrorOn(nameof(RegisterCommand.Email), RegisterCommands.Valid() with { Email = email });
    }

    [Fact]
    public void ErrorsInDifferentFields_AreReportedTogether()
    {
        // Alan içinde ilk hatada durulur (yukarıdaki boş e-posta tek hata verir), alanlar arasında durulmaz.
        var command = RegisterCommands.Valid() with { Email = "", FirstName = "" };

        var result = _validator.Validate(command);

        Assert.Equal([nameof(RegisterCommand.Email), nameof(RegisterCommand.FirstName)], result.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public void EmptyPassword_IsRejected()
    {
        AssertSingleErrorOn(nameof(RegisterCommand.Password), RegisterCommands.Valid() with { Password = "" });
    }

    [Theory]
    [InlineData("a")]          // kısa
    [InlineData("abcdefgh")]   // rakamsız
    public void WeakPassword_IsNotTheValidatorsJob(string password)
    {
        // Politika tek yerde, Identity ayarlarında (C3). Burada da kontrol edilseydi iki kaynak olurdu.
        Assert.True(_validator.Validate(RegisterCommands.Valid() with { Password = password }).IsValid);
    }

    [Theory]
    [InlineData(nameof(RegisterCommand.FirstName))]
    [InlineData(nameof(RegisterCommand.LastName))]
    public void EmptyName_IsRejected(string field)
    {
        AssertSingleErrorOn(field, WithName(field, ""));
    }

    [Theory]
    [InlineData(nameof(RegisterCommand.FirstName))]
    [InlineData(nameof(RegisterCommand.LastName))]
    public void NameLength_IsLimitedTo50(string field)
    {
        Assert.True(_validator.Validate(WithName(field, new string('a', RegisterCommandValidator.MaxNameLength))).IsValid);
        AssertSingleErrorOn(field, WithName(field, new string('a', RegisterCommandValidator.MaxNameLength + 1)));
    }

    [Fact]
    public void WhitespaceOnlyName_IsRejectedAfterNormalization()
    {
        // Handler önce normalize eder: "   " → "" → boş. Normalize olmadan da NotEmpty boşluğu boş sayar.
        var command = (RegisterCommands.Valid() with { FirstName = "   " }).Normalize();

        AssertSingleErrorOn(nameof(RegisterCommand.FirstName), command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("+90 555 123 45 67")]
    [InlineData("05551234567")]
    [InlineData("5")]
    [InlineData("+905551234567")]
    public void ValidPhoneNumber_IsAccepted(string? phoneNumber)
    {
        Assert.True(_validator.Validate(RegisterCommands.Valid() with { PhoneNumber = phoneNumber }).IsValid);
    }

    [Theory]
    [InlineData("+")]                    // rakam yok
    [InlineData("+ ")]                   // rakam yok
    [InlineData("90+5551234567")]        // '+' yalnızca başta
    [InlineData("++905551234567")]       // tek '+'
    [InlineData("0555-123-45-67")]       // tire kabul edilmez
    [InlineData("(0555) 1234567")]       // parantez kabul edilmez
    [InlineData("abc")]
    [InlineData("+90 555 123 45 67 890")] // 21 karakter
    public void InvalidPhoneNumber_IsRejected(string phoneNumber)
    {
        AssertHasErrorOn(nameof(RegisterCommand.PhoneNumber), RegisterCommands.Valid() with { PhoneNumber = phoneNumber });
    }

    [Fact]
    public void PhoneNumberOfExactly20Characters_IsAccepted()
    {
        var phoneNumber = "+90 555 123 45 67 89";
        Assert.Equal(RegisterCommandValidator.MaxPhoneNumberLength, phoneNumber.Length);

        Assert.True(_validator.Validate(RegisterCommands.Valid() with { PhoneNumber = phoneNumber }).IsValid);
    }

    [Fact]
    public void SkillLevelAndPosition_AreNotTheValidatorsJob()
    {
        // Seviye aralığı ve pozisyonun geçerliliği PlayerProfile'ın kuralı; handler onu domain'e bırakır.
        var command = RegisterCommands.Valid() with { SkillLevel = 0, PreferredPosition = (Position)99 };

        Assert.True(_validator.Validate(command).IsValid);
    }

    private static RegisterCommand WithName(string field, string value) => field switch
    {
        nameof(RegisterCommand.FirstName) => RegisterCommands.Valid() with { FirstName = value },
        nameof(RegisterCommand.LastName) => RegisterCommands.Valid() with { LastName = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    private void AssertSingleErrorOn(string propertyName, RegisterCommand command)
    {
        var result = _validator.Validate(command);
        Assert.Equal(propertyName, Assert.Single(result.Errors).PropertyName);
    }

    private void AssertHasErrorOn(string propertyName, RegisterCommand command)
    {
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal(propertyName, e.PropertyName));
    }
}
