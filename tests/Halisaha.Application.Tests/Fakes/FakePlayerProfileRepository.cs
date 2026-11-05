using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Domain.Entities;

namespace Halisaha.Application.Tests.Fakes;

public sealed class FakePlayerProfileRepository(CallLog log, FakeUnitOfWork unitOfWork) : IPlayerProfileRepository
{
    public List<PlayerProfile> Added { get; } = [];

    public void Add(PlayerProfile profile)
    {
        log.Record(unitOfWork.InTransaction ? "profiles.add (in transaction)" : "profiles.add (OUTSIDE transaction)");
        Added.Add(profile);
    }
}
