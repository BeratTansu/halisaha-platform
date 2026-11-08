namespace Halisaha.Api.Authentication;

/// <summary>
/// 401 cevabının <c>WWW-Authenticate</c> başlığı (RFC 6750 3, Karar 11). Değer tek yerde üretilir; JwtBearer
/// olayları ve ApiExceptionHandler bunu kullanır.
/// <list type="bullet">
/// <item>İstekte token yok (ör. login'de yanlış parola, korunan adrese token'sız istek): yalnızca <c>Bearer</c>.
/// RFC 6750 3.1: kimlik bilgisi gönderilmemişse hata kodu eklenmemeli.</item>
/// <item>Token gönderildi ama kabul edilmedi (bozuk, imzası yanlış, süresi dolmuş, <c>sub</c> geçersiz, sahibi
/// sistemde yok): <c>Bearer error="invalid_token"</c>.</item>
/// </list>
/// <c>error_description</c> bilerek yok: token'ın neden reddedildiğini (ör. "süresi şu saatte doldu") söylemez.
/// </summary>
internal static class BearerChallenge
{
    public const string Scheme = "Bearer";
    public const string InvalidToken = "Bearer error=\"invalid_token\"";

    public static string HeaderValue(bool tokenWasRejected) => tokenWasRejected ? InvalidToken : Scheme;
}
