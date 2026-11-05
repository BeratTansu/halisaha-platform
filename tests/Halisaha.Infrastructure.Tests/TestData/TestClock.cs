namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Sabit test zamanları. Offset 0 olmak zorunda (Npgsql timestamptz'ye yalnızca UTC yazar) ve tam saniye
/// seçildi; PostgreSQL mikrosaniye hassasiyetinde sakladığı için okunan değer yazılanla birebir eşit kalır.
/// </summary>
public static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset StartsAt = Now.AddDays(2);
}
