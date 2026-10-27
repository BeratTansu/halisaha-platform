using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using MatchType = Halisaha.Domain.Enums.MatchType;
using static Halisaha.Domain.Tests.TestData;

namespace Halisaha.Domain.Tests;

public class RequestPolicyTests
{
    private static readonly MatchRequest[] None = Array.Empty<MatchRequest>();

    [Fact]
    public void Player_can_apply_to_an_open_match()
    {
        var player = NewPlayer();
        var result = RequestPolicy.Send(OpenMatch(), player, RequestType.Application, None, Now);
        Assert.False(result.AcceptedExisting);
        Assert.Equal(RequestStatus.Pending, result.Request.Status);
        Assert.Equal(player, result.Request.PlayerId);
    }

    [Fact]
    public void Second_pending_request_of_same_type_is_rejected()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var first = RequestPolicy.Send(match, player, RequestType.Application, None, Now).Request;
        var ex = Assert.Throws<DomainException>(() =>
            RequestPolicy.Send(match, player, RequestType.Application, new[] { first }, Now));
        Assert.Equal("request.already_pending", ex.Code);
    }

    [Fact]
    public void Invitation_to_a_player_who_already_applied_accepts_the_application()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var application = RequestPolicy.Send(match, player, RequestType.Application, None, Now).Request;

        var result = RequestPolicy.Send(match, player, RequestType.Invitation, new[] { application }, Now);

        Assert.True(result.AcceptedExisting);
        Assert.Same(application, result.Request);
        Assert.Equal(RequestStatus.Accepted, application.Status);
        Assert.True(match.IsParticipant(player));
    }

    [Fact]
    public void Rejected_application_cannot_be_sent_again()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var application = RequestPolicy.Send(match, player, RequestType.Application, None, Now).Request;
        application.Reject(Organizer, Organizer, Now);

        var ex = Assert.Throws<DomainException>(() =>
            RequestPolicy.Send(match, player, RequestType.Application, new[] { application }, Now));
        Assert.Equal("request.rejected_before", ex.Code);
    }

    [Fact]
    public void Withdrawn_request_can_be_sent_again()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var application = RequestPolicy.Send(match, player, RequestType.Application, None, Now).Request;
        application.Withdraw(player, Organizer, Now);

        var again = RequestPolicy.Send(match, player, RequestType.Application, new[] { application }, Now);
        Assert.Equal(RequestStatus.Pending, again.Request.Status);
    }

    [Fact]
    public void Request_for_an_existing_participant_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() =>
            RequestPolicy.Send(OpenMatch(), Organizer, RequestType.Invitation, None, Now));
        Assert.Equal("request.already_participant", ex.Code);
    }

    [Fact]
    public void Accepting_when_full_fails_and_leaves_request_pending()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 2);
        var a = RequestPolicy.Send(match, NewPlayer(), RequestType.Application, None, Now).Request;
        var b = RequestPolicy.Send(match, NewPlayer(), RequestType.Application, None, Now).Request;

        RequestPolicy.AcceptBy(Organizer, match, a, Now);
        var ex = Assert.Throws<DomainException>(() => RequestPolicy.AcceptBy(Organizer, match, b, Now));

        Assert.Equal("match.full", ex.Code);
        Assert.Equal(RequestStatus.Pending, b.Status); // both or neither
    }

    [Fact]
    public void Only_the_receiving_side_can_accept()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var application = RequestPolicy.Send(match, player, RequestType.Application, None, Now).Request;
        var ex = Assert.Throws<DomainException>(() => RequestPolicy.AcceptBy(player, match, application, Now));
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    [Fact]
    public void Only_the_sender_can_withdraw_an_invitation()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        var invitation = RequestPolicy.Send(match, player, RequestType.Invitation, None, Now).Request;
        Assert.Throws<DomainException>(() => invitation.Withdraw(player, Organizer, Now));
        invitation.Withdraw(Organizer, Organizer, Now);
        Assert.Equal(RequestStatus.Withdrawn, invitation.Status);
    }

    [Fact]
    public void No_requests_after_the_start()
    {
        var ex = Assert.Throws<DomainException>(() =>
            RequestPolicy.Send(OpenMatch(), NewPlayer(), RequestType.Application, None, StartsAt));
        Assert.Equal("match.started", ex.Code);
    }
}
