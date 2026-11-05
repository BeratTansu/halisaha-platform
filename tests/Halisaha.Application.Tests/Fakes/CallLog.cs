namespace Halisaha.Application.Tests.Fakes;

/// <summary>
/// Sahte port'ların çağrılma sırası. Handler'ın sırası (ilçe → transaction → Identity → profil) birer kurala
/// dayandığı için testler yalnızca "çağrıldı mı"ya değil, "hangi sırayla ve transaction içinde mi"ye bakar.
/// </summary>
public sealed class CallLog
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Record(string call) => _calls.Add(call);
}
