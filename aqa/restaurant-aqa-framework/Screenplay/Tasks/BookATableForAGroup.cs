using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A new customer books a table of their own for a group, in one of the test locations. The table is
/// deliberately not deleted afterwards: the waiter's reservation list fails for a reservation whose table is gone
/// (see README), so cleanup cancels the reservation and leaves the table.</summary>
public sealed class BookATableForAGroup(string? name = null, int guests = 6, bool servedByANewWaiter = false) : ITask
{
    private const int MaxGroupSize = 6;

    public async Task PerformAsAsync(Actor actor)
    {
        var client = actor.UsingTheApi();
        var visitor = SampleDataFactory.RegisterRequest();
        var token = await RegisterAndSignInAsync(new AuthApi(client), visitor);

        var locationId = Settings.Current.RequireLocation(servedByANewWaiter);
        if (servedByANewWaiter)
            await HireAWaiterAsync(actor, locationId);

        var tableResponse = await new TablesApi(client).CreateTableAsync(
            SampleDataFactory.CreateTableRequest(locationId, MaxGroupSize));
        var table = await tableResponse.ReadAsAsync<TableResponse>();

        var reservations = new ReservationsApi(client);
        var startTime = DateTime.UtcNow.Date.AddDays(2).AddHours(12);
        var booked = await reservations.CreateReservationAsync(
            SampleDataFactory.CreateReservationRequest(table.Id, startTime, guests), token);
        if (!booked.Ok)
            throw new InvalidOperationException($"Booking the table failed with {booked.Status}: {await booked.TextAsync()}");
        var reservation = await booked.ReadAsAsync<ReservationResponse>();

        var groups = new GroupPreOrdersApi(client);
        var invite = await (await groups.GetInviteTokenAsync(reservation.Id, token)).ReadAsAsync<InviteTokenResponse>();
        var group = await (await groups.GetInviteAsync(invite.Token)).ReadAsAsync<GroupInviteResponse>();

        var booking = new GroupBooking(reservation.Id, group.GroupReservationId, locationId, visitor.Email, visitor.Password, token);
        if (name is null)
        {
            actor.Remember(booking);
        }
        else
        {
            // Remembering makes a booking the current one, and a named one must not push the scenario's own aside.
            var current = actor.Recall<GroupBooking>();
            actor.Remember(name, booking);
            actor.Remember(current);
        }

        // A new waiter's only reservation stays: cancelling could send the next booking to this waiter instead of a
        // newer one, as a reservation is given to the waiter with the fewest.
        if (!servedByANewWaiter)
        {
            actor.Recall<CleanupScope>().TrackDelete(
                $"reservation:{reservation.Id}",
                () => reservations.CancelReservationAsync(reservation.Id, token));
        }
    }

    /// <summary>The waiter's list asks for several things per reservation at once, which a low Lambda concurrency limit
    /// cannot serve for a long list. A waiter of one's own has just the one reservation, and being new they get it.</summary>
    private static async Task HireAWaiterAsync(Actor actor, string locationId)
    {
        var client = actor.UsingTheApi();
        var waiter = SampleDataFactory.RegisterRequest();
        await RegisterAndSignInAsync(new AuthApi(client), waiter);

        var hired = await new WaitersApi(client).AddWaiterAsync(new AddWaiterRequest(waiter.Email, locationId));
        if (!hired.Ok)
            throw new InvalidOperationException($"Hiring the waiter failed with {hired.Status}: {await hired.TextAsync()}");

        actor.Remember(new WaiterAccount(waiter.Email, waiter.Password));
    }

    private static async Task<string> RegisterAndSignInAsync(AuthApi auth, RegisterRequest visitor)
    {
        var registered = await auth.RegisterAsync(visitor);
        if (!registered.Ok)
            throw new InvalidOperationException($"Registering the customer failed with {registered.Status}: {await registered.TextAsync()}");

        var signedIn = await auth.SignInAsync(new SignInRequest(visitor.Email, visitor.Password));
        return (await signedIn.ReadAsAsync<SignInResponse>()).IdToken;
    }
}
