using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>`/waiter-reservations`, where a waiter lands after signing in, and the pre-order opened from a reservation card.</summary>
public sealed class WaiterReservationsPage(IPage page) : BasePage(page)
{
    public ILocator PreOrderDishes => Page.Locator("app-dish-card.pre-order-dish");
    public ILocator PreOrderTitle => Page.GetByRole(AriaRole.Heading, new() { NameRegex = new("^Pre-order of") });

    public ILocator Card => Page.Locator("li.card");

    public ILocator PreOrderButton() => Card.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true });
}
