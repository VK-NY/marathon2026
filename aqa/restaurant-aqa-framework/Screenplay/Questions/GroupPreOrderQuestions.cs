using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Seeding;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>A dish of the booked location's menu, by name. Those dishes are standing data (see README).</summary>
public sealed class TheDishOnTheTestMenu(string name) : IQuestion<DishListResponse>
{
    public async Task<DishListResponse> AnsweredByAsync(Actor actor)
    {
        var response = await new LocationsApi(actor.UsingTheApi()).GetDishesForLocationAsync(actor.Recall<GroupBooking>().LocationId);
        var dishes = await response.ReadAsAsync<IReadOnlyList<DishListResponse>>();

        return dishes.FirstOrDefault(dish => dish.Name == name)
            ?? throw new InvalidOperationException($"The test location has no dish '{name}'. See README, 'Standing test data'.");
    }
}

/// <summary>The token a caller's requests carry; null for a visitor who has not signed in.</summary>
public sealed class TheTokenOf(Caller caller) : IQuestion<string?>
{
    public async Task<string?> AnsweredByAsync(Actor actor) => caller switch
    {
        Caller.Owner => actor.Recall<GroupBooking>().OwnerToken,
        Caller.Waiter => (await actor.AsksForAsync(new TheWaiterSession())).IdToken,
        Caller.Guest => actor.RecallAll<GroupGuest>()[0].SessionToken,
        Caller.AnotherCustomer => await SignInAnotherCustomerAsync(actor),
        Caller.Visitor => null,
        _ => throw new InvalidOperationException($"Unknown caller {caller}."),
    };

    private static async Task<string> SignInAnotherCustomerAsync(Actor actor)
    {
        var auth = new AuthApi(actor.UsingTheApi());
        var customer = SampleDataFactory.RegisterRequest();
        await auth.RegisterAsync(customer);
        var signedIn = await auth.SignInAsync(new SignInRequest(customer.Email, customer.Password));
        return (await signedIn.ReadAsAsync<SignInResponse>()).IdToken;
    }
}

/// <summary>The group as the backend reports it to its Owner, whatever the last response was.</summary>
public sealed class TheGroupSnapshot(string? group = null) : IQuestion<GroupSnapshot>
{
    public async Task<GroupSnapshot> AnsweredByAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>(group);
        var response = await new GroupPreOrdersApi(actor.UsingTheApi()).GetSessionsAsync(booking.GroupId, booking.OwnerToken);
        return await response.ReadAsAsync<GroupSnapshot>();
    }
}

/// <summary>The order of the reservation, as the restaurant's waiter reads it.</summary>
public sealed class TheOrderTheWaiterGets : IQuestion<GetOrderResponse>
{
    public async Task<GetOrderResponse> AnsweredByAsync(Actor actor)
    {
        var token = await actor.AsksForAsync(new TheTokenOf(Caller.Waiter));
        var response = await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(actor.Recall<GroupBooking>().ReservationId, token);
        return await response.ReadAsAsync<GetOrderResponse>();
    }
}

/// <summary>Whether the group is still collecting or has been sent to the restaurant.</summary>
public sealed class TheGroupStatus : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var response = await new GroupPreOrdersApi(actor.UsingTheApi()).GetStatusAsync(actor.Recall<GroupBooking>().GroupId);
        var status = JsonSerializer.Deserialize<JsonElement>(await response.TextAsync()).GetProperty("status");
        return status.ValueKind == JsonValueKind.Number ? ((GroupState)status.GetInt32()).ToString() : status.GetString()!;
    }

    private enum GroupState
    {
        Open,
        Submitted,
        Closed,
    }
}
