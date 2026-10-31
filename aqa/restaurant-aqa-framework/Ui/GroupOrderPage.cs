using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>`/reservations/{id}/group-order`: the Owner's review of every participant's cart.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order" });
    public ILocator StatusBadge => Page.Locator(".heading-row .badge");
    public ILocator ErrorState => Page.GetByRole(AriaRole.Alert);
    public ILocator Notice => Page.Locator("p.notice");
    public ILocator EmptyState => Page.GetByText("Participants haven’t added any dishes yet");
    public ILocator ConsolidatedLines => Page.Locator(".consolidated-item");
    public ILocator GrandTotal => Page.Locator(".grand-total-value");
    public ILocator SubmitButton => Page.Locator("footer button.submit");
    public ILocator RefreshButton => Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
    public ILocator ConfirmSubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true });
    public ILocator ConfirmRemoveButton => Page.Locator("button.remove-confirm");

    public Task NavigateAsync(string reservationId) => NavigateToAsync($"/reservations/{reservationId}/group-order");

    public ParticipantCardComponent Card(string title) =>
        new(Page.Locator("app-participant-card").Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true }),
        }));
}
