using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Asks for the group's participants. A missing group id means a group that does not exist.</summary>
public sealed class AskForTheParticipants(Caller caller, string? groupId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var token = await actor.AsksForAsync(new TheTokenOf(caller));
        var group = groupId ?? actor.Recall<GroupBooking>().GroupId;
        await actor.TheLastResponse().CaptureAsync(await new GroupPreOrdersApi(actor.UsingTheApi()).GetSessionsAsync(group, token));
    }
}

/// <summary>Confirms the consolidated pre-order. Unknown ids are passed in by the scenarios that test them.</summary>
public sealed class ConfirmThePreOrder(Caller caller, string? groupId = null, string? reservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>();
        var token = await actor.AsksForAsync(new TheTokenOf(caller));
        await actor.TheLastResponse().CaptureAsync(await new GroupPreOrdersApi(actor.UsingTheApi())
            .SubmitGroupAsync(groupId ?? booking.GroupId, reservationId ?? booking.ReservationId, token));
    }
}

/// <summary>Removes a participant, identified by session id, from a group.</summary>
public sealed class RemoveTheParticipant(Caller caller, string sessionId, string? group = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var token = await actor.AsksForAsync(new TheTokenOf(caller));
        await actor.TheLastResponse().CaptureAsync(await new GroupPreOrdersApi(actor.UsingTheApi())
            .RemoveSessionAsync(actor.Recall<GroupBooking>(group).GroupId, sessionId, token));
    }
}

/// <summary>The Owner asks for their own session in the group, which also creates it the first time.</summary>
public sealed class AskForTheOwnerSession : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>();
        await actor.TheLastResponse().CaptureAsync(await new GroupPreOrdersApi(actor.UsingTheApi())
            .GetOwnerSessionAsync(booking.ReservationId, booking.OwnerToken));
    }
}
