using Halisaha.Application.Abstractions.Persistence;

namespace Halisaha.Application.Tests.Fakes;

public sealed class FakeLocationQueries(CallLog log, FakeUnitOfWork unitOfWork) : ILocationQueries
{
    public bool DistrictExists { get; set; } = true;

    public int? ReceivedDistrictId { get; private set; }

    public Task<bool> DistrictExistsAsync(int districtId, CancellationToken cancellationToken)
    {
        log.Record(unitOfWork.InTransaction ? "locations.district_exists (in transaction)" : "locations.district_exists");
        ReceivedDistrictId = districtId;
        return Task.FromResult(DistrictExists);
    }
}
