using Halisaha.Application.Commands.Login;

namespace Halisaha.Application.Tests.Commands.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void FilledFields_AreValid()
    {
        Assert.True(_validator.Validate(new LoginCommand("ahmet@example.com", "halisaha1")).IsValid);
    }

    [Fact]
    public void EmptyFields_AreReportedUnderTheirOwnNames()
    {
        var result = _validator.Validate(new LoginCommand("", ""));

        Assert.Equal(
            [nameof(LoginCommand.Email), nameof(LoginCommand.Password)],
            result.Errors.Select(e => e.PropertyName).Order());
    }

    [Theory]
    [InlineData("not-an-email", "x")]   // biçim kontrol edilmez: böyle bir hesap yoktur, sonuç 401 olur
    [InlineData("ahmet@example.com", "abc")] // politika kontrol edilmez: politika değişse de eski parola çalışmalı
    public void FormatAndPolicy_AreNotChecked(string email, string password)
    {
        Assert.True(_validator.Validate(new LoginCommand(email, password)).IsValid);
    }
}
