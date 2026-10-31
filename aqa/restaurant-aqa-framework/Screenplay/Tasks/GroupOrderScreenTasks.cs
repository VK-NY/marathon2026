using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The Owner signs in on the storefront and opens the group's screen.</summary>
public sealed class OpenTheGroupScreen : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(booking.OwnerEmail, booking.OwnerPassword));
        await new GroupOrderPage(actor.LookingAtThePage()).NavigateAsync(booking.ReservationId);
    }
}

/// <summary>The Owner opens the screen of a reservation that does not exist.</summary>
public sealed class OpenAGroupThatDoesNotExist : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(booking.OwnerEmail, booking.OwnerPassword));
        await new GroupOrderPage(actor.LookingAtThePage()).NavigateAsync(Guid.NewGuid().ToString());
    }
}

/// <summary>The Owner asks for fresh data instead of waiting for the next automatic update.</summary>
public sealed class RefreshTheGroupScreen : ITask
{
    public Task PerformAsAsync(Actor actor) => new GroupOrderPage(actor.LookingAtThePage()).RefreshButton.ClickAsync();
}

/// <summary>The Owner sends the consolidated pre-order to the restaurant, confirming the dialog.</summary>
public sealed class SendThePreOrderToTheRestaurant : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.SubmitButton.ClickAsync();
        await screen.ConfirmSubmitButton.ClickAsync();
    }
}

/// <summary>The Owner removes a guest from the group, confirming the dialog.</summary>
public sealed class RemoveTheGuestOnScreen(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.Card($"Guest: {guestName}").RemoveButton.ClickAsync();
        await screen.ConfirmRemoveButton.ClickAsync();
    }
}

/// <summary>The session ends and the waiter of this booking signs in on the same browser, then opens the pre-order of
/// this booking from their reservation list.</summary>
public sealed class OpenThePreOrderAsTheWaiter : ITask
{
    private const int MaxAttempts = 5;

    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        await page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }");

        var waiter = actor.Recall<WaiterAccount>();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(waiter.Email, waiter.Password));

        var reservations = new WaiterReservationsPage(page);
        await page.WaitForURLAsync("**/waiter-reservations");
        var preOrder = reservations.PreOrderButton();

        // The list asks for several things per reservation at once, and on a low Lambda concurrency limit some of
        // those requests are throttled: the card then renders without its pre-order. Only a reload brings it back.
        for (var attempt = 1; attempt < MaxAttempts && !await IsOfferedAsync(reservations, preOrder); attempt++)
            await page.ReloadAsync();

        await preOrder.ClickAsync();
        await reservations.PreOrderTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    private static async Task<bool> IsOfferedAsync(WaiterReservationsPage reservations, ILocator preOrder)
    {
        await reservations.Card.WaitForAsync();
        return await preOrder.IsVisibleAsync();
    }
}
