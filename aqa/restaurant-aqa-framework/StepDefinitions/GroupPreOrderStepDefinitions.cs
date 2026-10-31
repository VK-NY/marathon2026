using System.Globalization;
using Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>Group pre-order rules, independent of the screen: setting the group up, what the owner and others may do,
/// and how each outcome reads. Protocol details stop here; the feature files only say "refused" or "asked to sign in".</summary>
[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---- setting the group up ----

    [Given("a customer has booked a table for a group")]
    public Task GivenBooked() => Actor.AttemptsToAsync(new BookATableForAGroup());

    [Given("a customer has booked a table for a group that a waiter serves")]
    public Task GivenBookedForTheWaiter() => Actor.AttemptsToAsync(new BookATableForAGroup(servedByANewWaiter: true));

    [Given("another customer has booked a table for a group called {string}")]
    public Task GivenOtherGroup(string name) => Actor.AttemptsToAsync(new BookATableForAGroup(name));

    [Given("{string} has joined the group")]
    public Task GivenJoined(string guest) => Actor.AttemptsToAsync(new JoinTheGroup(guest));

    [Given("{string} has joined the group called {string}")]
    public Task GivenJoinedNamed(string guest, string group) => Actor.AttemptsToAsync(new JoinTheGroup(guest, group));

    [Given("{string} has added {int} {string} to their cart")]
    [When("{string} adds {int} {string} to their cart")]
    public Task GivenAdded(string guest, int quantity, string dish) => Actor.AttemptsToAsync(new AddToTheCart(guest, dish, quantity));

    [Given("{string} has confirmed their cart")]
    public async Task GivenConfirmed(string guest)
    {
        await Actor.AttemptsToAsync(new ConfirmTheCart(guest));
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded)
            throw new InvalidOperationException($"{guest} could not confirm: {outcome.Status} {outcome.Body}");
    }

    [When("{string} confirms their cart")]
    public Task WhenConfirms(string guest) => Actor.AttemptsToAsync(new ConfirmTheCart(guest));

    [Given("the owner has started their own order with {int} {string}")]
    public Task GivenOwnerOrder(int quantity, string dish) => Actor.AttemptsToAsync(new StartTheOwnersOrder(dish, quantity));

    [Given("a confirmed cart and the owner's order are ready")]
    public async Task GivenReady()
    {
        await GivenJoined("Anna");
        await GivenAdded("Anna", 1, "QA Soup");
        await GivenConfirmed("Anna");
        await GivenOwnerOrder(1, "QA Soup");
    }

    [Given("the owner has taken their own place in the group")]
    [When("the owner takes their own place in the group")]
    public Task WhenOwnPlace() => Actor.AttemptsToAsync(new AskForTheOwnerSession());

    // ---- viewing ----

    [When("the owner asks for the participants")]
    public Task WhenOwnerAsks() => Actor.AttemptsToAsync(new AskForTheParticipants(Caller.Owner));

    [When("{caller} asks for the participants")]
    public Task WhenAsks(Caller caller) => Actor.AttemptsToAsync(new AskForTheParticipants(caller));

    [When("the owner asks for the participants of a group that does not exist")]
    public Task WhenAsksMissing() => Actor.AttemptsToAsync(new AskForTheParticipants(Caller.Owner, Guid.NewGuid().ToString()));

    [Then("the participants are")]
    public async Task ThenParticipants(DataTable expected)
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupSnapshot());
        var guests = snapshot.Participants.Where(p => p.Type == "Guest").ToList();

        await Assert.That(guests.Count).IsEqualTo(expected.Rows.Count).Because($"group was: {Describe(snapshot)}");
        foreach (var row in expected.Rows)
        {
            var guest = guests.FirstOrDefault(p => p.Name == row["participant"]);
            await Assert.That(guest).IsNotNull().Because($"group was: {Describe(snapshot)}");
            await Assert.That(DishesOf(guest!)).IsEqualTo(row["dishes"]);
            await Assert.That(guest!.Total).IsEqualTo(Money(row["total"]));
            await Assert.That(guest.IsSubmitted).IsEqualTo(row["confirmed"] == "yes");
        }
    }

    [Then("the group total is {float}")]
    public async Task ThenGroupTotal(float total)
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupSnapshot());
        await Assert.That(snapshot.Total).IsEqualTo((decimal)total).Because($"group was: {Describe(snapshot)}");
        await Assert.That(snapshot.Participants.Sum(p => p.Total)).IsEqualTo(snapshot.Total);
    }

    [Then("the group has no participants")]
    public async Task ThenEmpty()
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupSnapshot());
        await Assert.That(snapshot.Participants.Count).IsEqualTo(0).Because($"group was: {Describe(snapshot)}");
        await Assert.That(snapshot.Total).IsEqualTo(0m);
    }

    [Then("the owner is listed as a participant")]
    public async Task ThenOwnerListed()
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupSnapshot());
        await Assert.That(snapshot.Participants.Count(p => p.Type == "Owner")).IsEqualTo(1).Because($"group was: {Describe(snapshot)}");
    }

    // ---- confirming ----

    [Given("the owner has confirmed the pre-order")]
    public async Task GivenOwnerConfirmed()
    {
        await WhenOwnerConfirms();
        await ThenAccepted();
    }

    [When("the owner confirms the pre-order")]
    public Task WhenOwnerConfirms() => Actor.AttemptsToAsync(new ConfirmThePreOrder(Caller.Owner));

    [When("{caller} confirms the pre-order")]
    public Task WhenConfirmsAs(Caller caller) => Actor.AttemptsToAsync(new ConfirmThePreOrder(caller));

    [When("the owner confirms the pre-order of a group that does not exist")]
    public Task WhenConfirmsMissingGroup() =>
        Actor.AttemptsToAsync(new ConfirmThePreOrder(Caller.Owner, groupId: Guid.NewGuid().ToString()));

    [When("the owner confirms the pre-order of a reservation that does not exist")]
    public Task WhenConfirmsMissingReservation() =>
        Actor.AttemptsToAsync(new ConfirmThePreOrder(Caller.Owner, reservationId: Guid.NewGuid().ToString()));

    [Then("the pre-order is accepted")]
    [Then("the removal is accepted")]
    public async Task ThenAccepted()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue().Because($"response status {outcome.Status}, body: {outcome.Body}");
    }

    [Then("the order holds")]
    public async Task ThenOrderHolds(DataTable expected)
    {
        var order = await Actor.AsksForAsync(new TheOrderTheWaiterGets());
        var dishes = order.OrderItems.Select(item => $"{item.Dish.Name} × {item.Quantity}").Order(StringComparer.Ordinal).ToList();
        await Assert.That(string.Join(", ", dishes))
            .IsEqualTo(string.Join(", ", expected.Rows.Select(row => row["dish"]).Order(StringComparer.Ordinal)));
    }

    [Then("the order has been handed to the restaurant")]
    public async Task ThenOrderHanded()
    {
        var order = await Actor.AsksForAsync(new TheOrderTheWaiterGets());
        await Assert.That(order.Status.ToString()).IsEqualTo("SubmittedByCustomer");
    }

    [Then("the group is marked as sent to the restaurant")]
    public async Task ThenGroupSent() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupStatus())).IsEqualTo("Submitted");

    // ---- removing ----

    [When("the owner removes {string}")]
    public Task WhenOwnerRemoves(string guest) =>
        Actor.AttemptsToAsync(new RemoveTheParticipant(Caller.Owner, Recall<GroupGuest>(guest).SessionId));

    [When("{caller} removes {string}")]
    public Task WhenRemovesAs(Caller caller, string guest) =>
        Actor.AttemptsToAsync(new RemoveTheParticipant(caller, Recall<GroupGuest>(guest).SessionId));

    [When("the owner removes somebody who never joined")]
    public Task WhenRemovesMissing() => Actor.AttemptsToAsync(new RemoveTheParticipant(Caller.Owner, Guid.NewGuid().ToString()));

    [When("the owner removes themselves")]
    public async Task WhenRemovesThemselves()
    {
        var own = (await Actor.AsksForAsync(new TheOutcome())).Body;
        var session = System.Text.Json.JsonSerializer.Deserialize<OwnerSessionResponse>(own, Api.JsonDefaults.Options)!;
        await Actor.AttemptsToAsync(new RemoveTheParticipant(Caller.Owner, session.GuestSessionId));
    }

    // ---- refusals: the one place that knows how they look on the wire ----

    [Then("{caller} is refused")]
    public Task ThenRefused(Caller caller) => ThenStatus(caller, 403);

    [Then("{caller} is asked to sign in")]
    public Task ThenAskedToSignIn(Caller caller) => ThenStatus(caller, 401);

    private async Task ThenStatus(Caller caller, int status)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status).Because($"{caller} got: {outcome.Body}");
    }

    [Then("the owner is told it could not be found")]
    public async Task ThenNotFound()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body was: {outcome.Body}");
    }

    [Then("the owner is told {string}")]
    [Scope(Tag = "api")]
    public async Task ThenOwnerIsTold(string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains(message);
    }

    [Then("the guest is turned down")]
    public async Task ThenGuestTurnedDown()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body was: {outcome.Body}");
    }

    private static string DishesOf(GroupParticipant participant) =>
        string.Join(", ", participant.Items.Select(item => $"{item.Dish.Name} × {item.Quantity}"));

    private static decimal Money(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static string Describe(GroupSnapshot snapshot) =>
        string.Join("; ", snapshot.Participants.Select(p => $"{p.Name} [{p.Type}] {DishesOf(p)} = {p.Total}")) + $" | total {snapshot.Total}";
}
