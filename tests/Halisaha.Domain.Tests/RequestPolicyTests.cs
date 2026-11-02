using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Tests;

public class RequestPolicyTests
{
    private static readonly Guid Organizer = TestData.Organizer;
    private static readonly Guid Mert = TestData.Mert;
    private static readonly Guid Emre = TestData.Emre;
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly MatchRequest[] NoHistory = [];

    private static MatchRequest MertApplies(Match match) => RequestPolicy.Apply(match, Mert, NoHistory, Now);
    private static MatchRequest OrganizerInvitesMert(Match match) => RequestPolicy.Invite(match, Organizer, Mert, NoHistory, Now);

    private static DomainException Fails(Action action, string code)
    {
        var ex = Assert.Throws<DomainException>(action);
        Assert.Equal(code, ex.Code);
        return ex;
    }

    // ---- Apply ----

    [Fact]
    public void Apply_WhenOpen_CreatesPendingApplication()
    {
        var match = TestData.OpenMatch();

        var request = MertApplies(match);

        Assert.Equal(RequestType.Application, request.Type);
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Equal(match.Id, request.MatchId);
        Assert.Equal(Mert, request.PlayerId);
        Assert.False(match.IsParticipant(Mert)); // sending never changes the roster
    }

    [Fact]
    public void Apply_WithEmptyPlayer_ThrowsPlayerRequired()
    {
        var ex = Fails(() => RequestPolicy.Apply(TestData.OpenMatch(), Guid.Empty, NoHistory, Now), "match.player_required");
        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public void Apply_ToCancelledMatch_ThrowsNotOpen()
    {
        var match = TestData.OpenMatch();
        match.Cancel(Organizer, Now);

        Fails(() => MertApplies(match), "match.not_open");
    }

    // Exactly at kick-off counts as started.
    [Fact]
    public void Apply_AtStartTime_ThrowsStarted()
    {
        Fails(() => RequestPolicy.Apply(TestData.OpenMatch(), Mert, NoHistory, TestData.StartsAt), "match.started");
    }

    [Fact]
    public void Apply_WhenAlreadyParticipant_ThrowsAlreadyParticipant()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, Now);

        Fails(() => MertApplies(match), "match.already_participant");
    }

    // The organizer joins the roster when the match is created.
    [Fact]
    public void Apply_ByOrganizerToOwnMatch_ThrowsAlreadyParticipant()
    {
        Fails(() => RequestPolicy.Apply(TestData.OpenMatch(), Organizer, NoHistory, Now), "match.already_participant");
    }

    [Fact]
    public void Apply_WithOwnPendingApplication_ThrowsPendingExists()
    {
        var match = TestData.OpenMatch();
        var pending = MertApplies(match);

        Fails(() => RequestPolicy.Apply(match, Mert, [pending], Now), "request.pending_exists");
    }

    // No implicit acceptance: Mert must accept the invitation explicitly.
    [Fact]
    public void Apply_WithPendingInvitation_ThrowsPendingExists()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        Fails(() => RequestPolicy.Apply(match, Mert, [invitation], Now), "request.pending_exists");
        Assert.Equal(RequestStatus.Pending, invitation.Status);
        Assert.False(match.IsParticipant(Mert));
    }

    [Fact]
    public void Apply_AfterRejectedApplication_ThrowsRejectedBefore()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Reject(match, application, Organizer, Now);

        Fails(() => RequestPolicy.Apply(match, Mert, [application], Now), "request.rejected_before");
    }

    // Mert said no to the invitation; he can still open the door himself.
    [Fact]
    public void Apply_AfterRejectedInvitation_CreatesApplication()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);
        RequestPolicy.Reject(match, invitation, Mert, Now);

        var application = RequestPolicy.Apply(match, Mert, [invitation], Now);

        Assert.Equal(RequestStatus.Pending, application.Status);
    }

    [Fact]
    public void Apply_AfterWithdrawnApplication_CreatesApplication()
    {
        var match = TestData.OpenMatch();
        var first = MertApplies(match);
        RequestPolicy.Withdraw(match, first, Mert, Now);

        var second = RequestPolicy.Apply(match, Mert, [first], Now);

        Assert.Equal(RequestStatus.Pending, second.Status);
    }

    // Mert left the match; the accepted request does not block a new one.
    [Fact]
    public void Apply_AfterAcceptedAndLeft_CreatesApplication()
    {
        var match = TestData.OpenMatch();
        var first = MertApplies(match);
        RequestPolicy.Accept(match, first, Organizer, Now);
        match.Withdraw(Mert, Now);

        var second = RequestPolicy.Apply(match, Mert, [first], Now);

        Assert.Equal(RequestStatus.Pending, second.Status);
    }

    // Rejected application + pending invitation: the current state wins, so Mert is led to the invitation.
    [Fact]
    public void Apply_WithPendingInvitationAndRejectedApplication_ThrowsPendingExists()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Reject(match, application, Organizer, Now);
        var invitation = RequestPolicy.Invite(match, Organizer, Mert, [application], Now);

        Fails(() => RequestPolicy.Apply(match, Mert, [application, invitation], Now), "request.pending_exists");
    }

    [Fact]
    public void Apply_WithAnotherPlayersHistory_ThrowsInvalidOperation()
    {
        var match = TestData.OpenMatch();
        var emresRequest = RequestPolicy.Apply(match, Emre, NoHistory, Now);

        Assert.Throws<InvalidOperationException>(() => RequestPolicy.Apply(match, Mert, [emresRequest], Now));
    }

    // ---- Invite ----

    [Fact]
    public void Invite_ByOrganizer_CreatesPendingInvitation()
    {
        var request = OrganizerInvitesMert(TestData.OpenMatch());

        Assert.Equal(RequestType.Invitation, request.Type);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Invite_ByStranger_ThrowsNotOrganizer()
    {
        var ex = Fails(() => RequestPolicy.Invite(TestData.OpenMatch(), Emre, Mert, NoHistory, Now), "match.not_organizer");
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    // Shape comes before Who.
    [Fact]
    public void Invite_ByStrangerWithEmptyPlayer_ThrowsPlayerRequired()
    {
        Fails(() => RequestPolicy.Invite(TestData.OpenMatch(), Emre, Guid.Empty, NoHistory, Now), "match.player_required");
    }

    // Who comes before When: a stranger learns nothing about the match state.
    [Fact]
    public void Invite_ByStrangerToCancelledMatch_ThrowsNotOrganizer()
    {
        var match = TestData.OpenMatch();
        match.Cancel(Organizer, Now);

        Fails(() => RequestPolicy.Invite(match, Emre, Mert, NoHistory, Now), "match.not_organizer");
    }

    [Fact]
    public void Invite_Self_ThrowsAlreadyParticipant()
    {
        Fails(() => RequestPolicy.Invite(TestData.OpenMatch(), Organizer, Organizer, NoHistory, Now), "match.already_participant");
    }

    [Fact]
    public void Invite_AfterRejectedInvitation_ThrowsRejectedBefore()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);
        RequestPolicy.Reject(match, invitation, Mert, Now);

        Fails(() => RequestPolicy.Invite(match, Organizer, Mert, [invitation], Now), "request.rejected_before");
    }

    // The organizer said no to the application; he can change his mind and invite.
    [Fact]
    public void Invite_AfterRejectedApplication_CreatesInvitation()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Reject(match, application, Organizer, Now);

        var invitation = RequestPolicy.Invite(match, Organizer, Mert, [application], Now);

        Assert.Equal(RequestStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Invite_WithPendingApplication_ThrowsPendingExists()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        Fails(() => RequestPolicy.Invite(match, Organizer, Mert, [application], Now), "request.pending_exists");
        Assert.False(match.IsParticipant(Mert));
    }

    // ---- Accept ----

    [Fact]
    public void Accept_ApplicationByOrganizer_AddsPlayerAndClosesRequest()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        var acceptedAt = Now.AddHours(1);

        RequestPolicy.Accept(match, application, Organizer, acceptedAt);

        Assert.True(match.IsParticipant(Mert));
        Assert.Equal(RequestStatus.Accepted, application.Status);
        Assert.Equal(acceptedAt, application.ClosedAt);
    }

    [Fact]
    public void Accept_InvitationByPlayer_AddsPlayer()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        RequestPolicy.Accept(match, invitation, Mert, Now);

        Assert.True(match.IsParticipant(Mert));
        Assert.Equal(RequestStatus.Accepted, invitation.Status);
    }

    [Fact]
    public void Accept_ApplicationByStranger_ThrowsNotResponder()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        var ex = Fails(() => RequestPolicy.Accept(match, application, Emre, Now), "request.not_responder");
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    // Mert cannot accept his own application.
    [Fact]
    public void Accept_ApplicationByApplicant_ThrowsNotResponder()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        Fails(() => RequestPolicy.Accept(match, application, Mert, Now), "request.not_responder");
    }

    // The organizer cannot accept his own invitation on Mert's behalf.
    [Fact]
    public void Accept_InvitationByOrganizer_ThrowsNotResponder()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        Fails(() => RequestPolicy.Accept(match, invitation, Organizer, Now), "request.not_responder");
    }

    // Capacity 2, no external players: Mert fills the last spot, Emre's application waits.
    [Fact]
    public void Accept_WhenFull_ThrowsFullAndKeepsRequestPending()
    {
        var match = TestData.OpenMatch(capacity: 2, externalPlayerCount: 0);
        var mertsApplication = MertApplies(match);
        var emresApplication = RequestPolicy.Apply(match, Emre, NoHistory, Now);
        RequestPolicy.Accept(match, mertsApplication, Organizer, Now);

        Fails(() => RequestPolicy.Accept(match, emresApplication, Organizer, Now), "match.full");

        Assert.Equal(RequestStatus.Pending, emresApplication.Status);
        Assert.Null(emresApplication.ClosedAt);
        Assert.False(match.IsParticipant(Emre));
    }

    [Fact]
    public void Accept_AtStartTime_ThrowsStarted()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        Fails(() => RequestPolicy.Accept(match, application, Organizer, TestData.StartsAt), "match.started");
    }

    [Fact]
    public void Accept_ClosedRequest_ThrowsNotPendingAndLeavesRosterUnchanged()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Withdraw(match, application, Mert, Now);

        Fails(() => RequestPolicy.Accept(match, application, Organizer, Now), "request.not_pending");
        Assert.False(match.IsParticipant(Mert));
    }

    // When comes before What: a withdrawn request on a started match reports the match state.
    [Fact]
    public void Accept_ClosedRequestAfterStart_ThrowsStarted()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Withdraw(match, application, Mert, Now);

        Fails(() => RequestPolicy.Accept(match, application, Organizer, TestData.StartsAt), "match.started");
    }

    [Fact]
    public void Accept_RequestOfAnotherMatch_ThrowsInvalidOperation()
    {
        var application = MertApplies(TestData.OpenMatch());

        Assert.Throws<InvalidOperationException>(() => RequestPolicy.Accept(TestData.OpenMatch(), application, Organizer, Now));
    }

    // ---- Reject ----

    [Fact]
    public void Reject_ApplicationByOrganizer_ClosesRequest()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        RequestPolicy.Reject(match, application, Organizer, Now);

        Assert.Equal(RequestStatus.Rejected, application.Status);
        Assert.Equal(Now, application.ClosedAt);
    }

    [Fact]
    public void Reject_ApplicationByStranger_ThrowsNotResponder()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        Fails(() => RequestPolicy.Reject(match, application, Emre, Now), "request.not_responder");
    }

    // The organizer withdraws his invitation; he does not reject it.
    [Fact]
    public void Reject_InvitationByOrganizer_ThrowsNotResponder()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        Fails(() => RequestPolicy.Reject(match, invitation, Organizer, Now), "request.not_responder");
    }

    [Fact]
    public void Reject_WhenFull_StillRejects()
    {
        var match = TestData.OpenMatch(capacity: 2, externalPlayerCount: 0);
        var emresApplication = RequestPolicy.Apply(match, Emre, NoHistory, Now);
        match.AddParticipant(Mert, Now);

        RequestPolicy.Reject(match, emresApplication, Organizer, Now);

        Assert.Equal(RequestStatus.Rejected, emresApplication.Status);
    }

    [Fact]
    public void Reject_OnCancelledMatch_ThrowsNotOpen()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        match.Cancel(Organizer, Now);

        Fails(() => RequestPolicy.Reject(match, application, Organizer, Now), "match.not_open");
        Assert.Equal(RequestStatus.Pending, application.Status);
    }

    [Fact]
    public void Reject_ClosedRequest_ThrowsNotPending()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Reject(match, application, Organizer, Now);

        Fails(() => RequestPolicy.Reject(match, application, Organizer, Now), "request.not_pending");
    }

    // ---- Withdraw ----

    [Fact]
    public void Withdraw_ApplicationByApplicant_ClosesRequest()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        RequestPolicy.Withdraw(match, application, Mert, Now);

        Assert.Equal(RequestStatus.Withdrawn, application.Status);
        Assert.Equal(Now, application.ClosedAt);
    }

    [Fact]
    public void Withdraw_InvitationByOrganizer_ClosesRequest()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        RequestPolicy.Withdraw(match, invitation, Organizer, Now);

        Assert.Equal(RequestStatus.Withdrawn, invitation.Status);
    }

    // The organizer rejects an application; he does not withdraw it.
    [Fact]
    public void Withdraw_ApplicationByOrganizer_ThrowsNotCreator()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);

        var ex = Fails(() => RequestPolicy.Withdraw(match, application, Organizer, Now), "request.not_creator");
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    [Fact]
    public void Withdraw_InvitationByPlayer_ThrowsNotCreator()
    {
        var match = TestData.OpenMatch();
        var invitation = OrganizerInvitesMert(match);

        Fails(() => RequestPolicy.Withdraw(match, invitation, Mert, Now), "request.not_creator");
    }

    // When comes before What.
    [Fact]
    public void Withdraw_ClosedRequestAfterStart_ThrowsStarted()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Withdraw(match, application, Mert, Now);

        Fails(() => RequestPolicy.Withdraw(match, application, Mert, TestData.StartsAt), "match.started");
    }

    [Fact]
    public void Withdraw_ClosedRequest_ThrowsNotPending()
    {
        var match = TestData.OpenMatch();
        var application = MertApplies(match);
        RequestPolicy.Withdraw(match, application, Mert, Now);

        Fails(() => RequestPolicy.Withdraw(match, application, Mert, Now), "request.not_pending");
    }
}
