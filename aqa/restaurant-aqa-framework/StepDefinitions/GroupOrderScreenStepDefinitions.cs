using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>The owner's group screen and the waiter's view of the order, as a user sees them.</summary>
[Binding]
public sealed class GroupOrderScreenStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("the owner has opened the group's screen")]
    [When("the owner opens the group's screen")]
    public Task WhenOpens() => Actor.AttemptsToAsync(new OpenTheGroupScreen());

    [When("the owner opens the screen of a reservation that does not exist")]
    public Task WhenOpensMissing() => Actor.AttemptsToAsync(new OpenAGroupThatDoesNotExist());

    [When("the owner refreshes the screen")]
    public Task WhenRefreshes() => Actor.AttemptsToAsync(new RefreshTheGroupScreen());

    [When("the owner sends the pre-order to the restaurant")]
    public Task WhenSends() => Actor.AttemptsToAsync(new SendThePreOrderToTheRestaurant());

    [When("the owner removes {string} on the screen")]
    public async Task WhenRemoves(string guest)
    {
        await Actor.AttemptsToAsync(new RemoveTheGuestOnScreen(guest));
        await Actor.AsksForAsync(new TheParticipantIsGone($"Guest: {guest}"));
    }

    [When("the waiter opens the pre-order of this reservation")]
    public Task WhenWaiterOpens() => Actor.AttemptsToAsync(new OpenThePreOrderAsTheWaiter());

    [Then("{string} is shown as {string} with subtotal {string} and dishes")]
    public async Task ThenShown(string title, string status, string subtotal, DataTable dishes)
    {
        var participant = await Actor.AsksForAsync(new TheParticipantShown(title, subtotal));

        await Assert.That(participant.Status).IsEqualTo(status);
        await Assert.That(participant.Subtotal).IsEqualTo(subtotal);
        await Assert.That(string.Join(", ", participant.Dishes.Order(StringComparer.Ordinal)))
            .IsEqualTo(string.Join(", ", dishes.Rows.Select(row => row["dish"]).Order(StringComparer.Ordinal)));
    }

    [Then("{string} is shown as {string} without dishes")]
    public async Task ThenShownWithoutDishes(string title, string status)
    {
        var participant = await Actor.AsksForAsync(new TheParticipantShown(title));

        await Assert.That(participant.Status).IsEqualTo(status);
        await Assert.That(participant.Dishes.Count).IsEqualTo(0);
        await Assert.That(participant.Subtotal).IsEqualTo("$0.00");
    }

    [Then("{string} is no longer shown")]
    public async Task ThenGone(string title) =>
        await Assert.That(await Actor.AsksForAsync(new TheParticipantIsGone(title))).IsTrue();

    [Then("the owner is warned that {int} participants have not confirmed")]
    public async Task ThenWarned(int count) =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupScreenMessage(GroupScreenMessage.Notice)))
            .StartsWith($"{count} participants");

    [Then("the grand total shown is {string}")]
    public async Task ThenGrandTotal(string total) =>
        await Assert.That(await Actor.AsksForAsync(new TheGrandTotalShown(total))).IsEqualTo(total);

    [Then("the owner is told nobody has added dishes yet")]
    public async Task ThenNobodyYet() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupScreenMessage(GroupScreenMessage.Empty)))
            .IsEqualTo("Participants haven’t added any dishes yet");

    [Then("the owner is told the group could not be found")]
    public async Task ThenGroupNotFound() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupScreenMessage(GroupScreenMessage.Error)))
            .Contains("Group reservation not found");

    [Then("the owner cannot send the pre-order")]
    public async Task ThenCannotSend() =>
        await Assert.That(await Actor.AsksForAsync(new TheSubmitButtonIsEnabled())).IsFalse();

    [Then("the consolidated list shows")]
    public async Task ThenConsolidated(DataTable dishes) =>
        await Assert.That(string.Join(", ", await Actor.AsksForAsync(new TheConsolidatedDishes())))
            .IsEqualTo(string.Join(", ", dishes.Rows.Select(row => row["dish"]).Order(StringComparer.Ordinal)));

    [Then("the owner is told {string}")]
    [Scope(Tag = "ui")]
    public async Task ThenToast(string message) =>
        await Assert.That(await Actor.AsksForAsync(new TheSuccessToast())).IsEqualTo(message);

    [When("the screen says {string}")]
    [Then("the screen says {string}")]
    public async Task ThenBadge(string status) =>
        await Assert.That(await Actor.AsksForAsync(new TheStatusBadgeShown(status))).IsEqualTo(status);

    [Then("the waiter sees")]
    public async Task ThenWaiterSees(DataTable dishes) =>
        await Assert.That(string.Join(", ", await Actor.AsksForAsync(new ThePreOrderTheWaiterSees())))
            .IsEqualTo(string.Join(", ", dishes.Rows.Select(row => row["dish"]).Order(StringComparer.Ordinal)));
}
