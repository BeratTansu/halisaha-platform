using Halisaha.Application.Commands.Login;

namespace Halisaha.Application.Tests.Commands.Login;

public class LoginCommandTests
{
    [Fact]
    public void Normalize_TrimsEmail_ButNotPassword()
    {
        // Register e-postayı kırpıp saklar; login kırpmasaydı " ahmet@x.com" ile giriş yapılamazdı.
        // Parolanın boşluğu parolanın parçasıdır (Register da kırpmaz).
        var command = new LoginCommand("  ahmet@example.com ", " halisaha1 ");

        var normalized = command.Normalize();

        Assert.Equal("ahmet@example.com", normalized.Email);
        Assert.Equal(" halisaha1 ", normalized.Password);
    }

    [Fact]
    public void Normalize_TurnsMissingValuesIntoEmpty()
    {
        var command = new LoginCommand(null!, null!);

        var normalized = command.Normalize();

        Assert.Equal(string.Empty, normalized.Email);
        Assert.Equal(string.Empty, normalized.Password);
    }

    [Fact]
    public void ToString_DoesNotContainThePassword()
    {
        var command = new LoginCommand("ahmet@example.com", "cok-gizli-parola-42");

        var text = command.ToString();

        Assert.DoesNotContain("cok-gizli-parola-42", text);
        Assert.Contains("Password = ***", text);
    }
}
