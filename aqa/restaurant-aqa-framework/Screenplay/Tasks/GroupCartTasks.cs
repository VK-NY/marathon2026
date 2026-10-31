using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest joins a group through the invitation and is remembered by name.</summary>
public sealed class JoinTheGroup(string guestName, string? group = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>(group);
        var response = await new GroupPreOrdersApi(actor.UsingTheApi()).JoinAsync(new JoinGroupRequest(guestName, booking.GroupId));
        if (!response.Ok)
            throw new InvalidOperationException($"{guestName} could not join: {response.Status} {await response.TextAsync()}");

        var session = await response.ReadAsAsync<GuestSessionResponse>();
        actor.Remember(guestName, new GroupGuest(guestName, session.SessionId, session.SessionToken));
    }
}

/// <summary>A guest adds a dish of the test location's menu to their own cart.</summary>
public sealed class AddToTheCart(string guestName, string dishName, int quantity) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GroupGuest>(guestName);
        var dish = await actor.AsksForAsync(new TheDishOnTheTestMenu(dishName));
        var response = await new GroupPreOrdersApi(actor.UsingTheApi())
            .AddDishAsync(guest.SessionId, new CartDishRequest(dish.Id, quantity), guest.SessionToken);
        if (!response.Ok)
            throw new InvalidOperationException($"{guestName} could not add {dishName}: {response.Status} {await response.TextAsync()}");
    }
}

/// <summary>A guest confirms their cart. The outcome is the scenario's last response, as a refusal may be the point.</summary>
public sealed class ConfirmTheCart(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GroupGuest>(guestName);
        await actor.TheLastResponse().CaptureAsync(
            await new GroupPreOrdersApi(actor.UsingTheApi()).SubmitCartAsync(guest.SessionId, guest.SessionToken));
    }
}

/// <summary>The Owner starts their own pre-order. The group can only be confirmed once this order exists.</summary>
public sealed class StartTheOwnersOrder(string dishName, int quantity) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>();
        var dish = await actor.AsksForAsync(new TheDishOnTheTestMenu(dishName));
        var response = await new OrdersApi(actor.UsingTheApi()).CreateOrderAsync(
            new CreateOrderRequest(booking.ReservationId, [new OrderItemRequest(dish.Id, quantity)]),
            booking.OwnerToken);
        if (!response.Ok)
            throw new InvalidOperationException($"The Owner's order failed: {response.Status} {await response.TextAsync()}");
    }
}
