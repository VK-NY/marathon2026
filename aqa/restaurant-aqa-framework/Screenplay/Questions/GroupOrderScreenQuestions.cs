using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>Gives the page the usual time to show a text. Not finding it is no error here: the step that asked reports
/// the difference, with the value that is on the page.</summary>
internal static class ExpectedText
{
    public static async Task WaitForAsync(ILocator locator, string text)
    {
        try
        {
            await Assertions.Expect(locator).ToHaveTextAsync(text, new() { Timeout = Settings.Current.DefaultTimeoutMs });
        }
        catch (PlaywrightException)
        {
        }
    }
}

/// <summary>What the screen shows for one participant. "dish × 2 $25.00" lines are read as plain text.</summary>
public sealed record ParticipantShown(string Status, IReadOnlyList<string> Dishes, string Subtotal, bool CanBeRemoved);

/// <summary>A subtotal, when given, is waited for: after a refresh the old values stay until the server has answered.</summary>
public sealed class TheParticipantShown(string title, string? expectedSubtotal = null) : IQuestion<ParticipantShown>
{
    public async Task<ParticipantShown> AnsweredByAsync(Actor actor)
    {
        var card = new GroupOrderPage(actor.LookingAtThePage()).Card(title);
        await card.Status.WaitForAsync();
        if (expectedSubtotal is not null)
            await ExpectedText.WaitForAsync(card.Subtotal, expectedSubtotal);

        var lines = await card.Items.AllInnerTextsAsync();
        return new ParticipantShown(
            await card.Status.InnerTextAsync(),
            lines.Select(Squeeze).ToList(),
            await card.Subtotal.InnerTextAsync(),
            await card.RemoveButton.IsVisibleAsync());
    }

    // A card line stacks the initial, name, quantity and price; this leaves "QA Salad × 2 $25.00".
    private static string Squeeze(string line)
    {
        var parts = line.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts.Where(part => part.Length > 1));
    }
}

/// <summary>Whether a participant's card has left the screen, waited for because it goes after a server reply.</summary>
public sealed class TheParticipantIsGone(string title) : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        await new GroupOrderPage(actor.LookingAtThePage()).Card(title).Root
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        return true;
    }
}

/// <summary>The amount in the footer, once it has become the expected one or the wait has run out.</summary>
public sealed class TheGrandTotalShown(string? expected = null) : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var total = new GroupOrderPage(actor.LookingAtThePage()).GrandTotal;
        if (expected is not null)
            await ExpectedText.WaitForAsync(total, expected);

        return await total.InnerTextAsync();
    }
}

/// <summary>"Collecting pre-orders" or "Sent to Restaurant", once it has become the expected one.</summary>
public sealed class TheStatusBadgeShown(string expected) : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var badge = new GroupOrderPage(actor.LookingAtThePage()).StatusBadge;
        await ExpectedText.WaitForAsync(badge, expected);
        return await badge.InnerTextAsync();
    }
}

/// <summary>The lines of the consolidated list, as "QA Salad × 3".</summary>
public sealed class TheConsolidatedDishes : IQuestion<IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> AnsweredByAsync(Actor actor)
    {
        var lines = new GroupOrderPage(actor.LookingAtThePage()).ConsolidatedLines;
        await lines.First.WaitForAsync();
        var texts = await lines.AllInnerTextsAsync();
        return texts.Select(text => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim()).Order(StringComparer.Ordinal).ToList();
    }
}

/// <summary>What the Owner's screen says about the state of the group.</summary>
public sealed class TheGroupScreenMessage(GroupScreenMessage message) : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        var locator = message switch
        {
            GroupScreenMessage.Notice => screen.Notice,
            GroupScreenMessage.Empty => screen.EmptyState,
            GroupScreenMessage.Error => screen.ErrorState,
            _ => throw new InvalidOperationException($"Unknown message {message}."),
        };

        await locator.WaitForAsync();
        return await locator.InnerTextAsync();
    }
}

public enum GroupScreenMessage
{
    Notice,
    Empty,
    Error,
}

/// <summary>Whether the Owner can send the pre-order right now.</summary>
public sealed class TheSubmitButtonIsEnabled : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        var button = new GroupOrderPage(actor.LookingAtThePage()).SubmitButton;
        await button.WaitForAsync();
        return await button.IsEnabledAsync();
    }
}

/// <summary>The text of the green confirmation that pops up.</summary>
public sealed class TheSuccessToast : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var toast = new ToastComponent(actor.LookingAtThePage()).Success;
        await toast.WaitForAsync();
        return await toast.InnerTextAsync();
    }
}

/// <summary>The dishes on the waiter's pre-order page, as "QA Salad × 3".</summary>
public sealed class ThePreOrderTheWaiterSees : IQuestion<IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> AnsweredByAsync(Actor actor)
    {
        var dishes = new WaiterReservationsPage(actor.LookingAtThePage()).PreOrderDishes;
        await dishes.First.WaitForAsync();

        var lines = new List<string>();
        foreach (var dish in await dishes.AllAsync())
        {
            var name = await dish.Locator(".pre-order-quantity").Locator("xpath=ancestor::app-dish-card").InnerTextAsync();
            lines.Add(ToLine(name));
        }

        return lines.Order(StringComparer.Ordinal).ToList();
    }

    private static string ToLine(string cardText)
    {
        var parts = cardText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts.First(part => part.StartsWith("QA ", StringComparison.Ordinal));
        var quantity = parts.First(part => part.StartsWith("Quantity:", StringComparison.Ordinal))["Quantity:".Length..].Trim();
        return $"{name} × {quantity}";
    }
}
