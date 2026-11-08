namespace Halisaha.Application.Queries.GetMe;

/// <summary>
/// "Ben kimim?" sorgusu. <see cref="ActorId"/> token'dan gelir (L2b): controller <c>User.GetActorId()</c> ile
/// okur, Application token'ı görmez. L2b'nin ilk kullanıcısı.
/// </summary>
public sealed record GetMeQuery(Guid ActorId);
