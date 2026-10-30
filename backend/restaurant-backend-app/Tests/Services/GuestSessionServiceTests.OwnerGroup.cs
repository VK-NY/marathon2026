using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

// Owner-side management of a group pre-order: list, owner session id, delete.
public partial class GuestSessionServiceTests
{
    private const string OwnerId = "owner-user";
    private const string GroupId = "group-1";
    private const string ReservationId = "reservation-1";

    private void SetupOwnedGroup(GroupReservationStatus status = GroupReservationStatus.Open, string customerId = OwnerId)
    {
        var group = new GroupReservation
        {
            GroupReservationId = GroupId,
            ReservationId = ReservationId,
            ReservationToken = "token",
            Status = status
        };

        var reservation = new Reservation
        {
            Id = ReservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = customerId,
            GuestCount = 4,
            Status = ReservationStatus.Reserved
        };

        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync(group);
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(ReservationId)).ReturnsAsync(group);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(ReservationId)).ReturnsAsync(reservation);
    }

    private void SetupDishes()
    {
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(
            [
                new Dish { Id = "dish1", Name = "Caesar Salad", Price = 180 },
                new Dish { Id = "dish2", Name = "Tom Yum", Price = 220 }
            ]);
    }

    private static GuestSession Session(string id, string name, bool submitted, params (string dishId, int quantity)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = GroupId,
        GuestName = name,
        IsSubmitted = submitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.dishId, Quantity = d.quantity, State = OrderItemState.Pending }).ToList()
    };

    [Fact]
    public async Task GetGroupSessionsAsync_Owner_ReturnsParticipantsWithTotals()
    {
        SetupOwnedGroup();
        SetupDishes();

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(
            [
                Session("session1", "Andrii", true, ("dish1", 2)),
                Session("session2", "Olena", false, ("dish2", 1))
            ]);

        var result = await _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId);

        var participants = result.Participants.ToList();
        Assert.Equal(2, participants.Count);
        Assert.Equal(580, result.Total);

        Assert.Equal("session1", participants[0].Id);
        Assert.Equal(ParticipantType.Guest, participants[0].Type);
        Assert.Equal("Andrii", participants[0].Name);
        Assert.Equal(360, participants[0].Total);
        Assert.True(participants[0].IsSubmitted);
        Assert.Equal("Caesar Salad", participants[0].Items.Single().Dish.Name);
        Assert.Equal(2, participants[0].Items.Single().Quantity);

        Assert.Equal(220, participants[1].Total);
        Assert.False(participants[1].IsSubmitted);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_OwnerSessionPresent_ListedFirstAsOwnerWithRealName()
    {
        SetupOwnedGroup();
        SetupDishes();

        var ownerSession = Session("owner-session", "Owner", false, ("dish2", 1));
        ownerSession.IsOwnerSession = true;
        ownerSession.OwnerUserId = OwnerId;

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([Session("session1", "Andrii", true, ("dish1", 1)), ownerSession]);

        var user = new UserType { Attributes = [new AttributeType { Name = "given_name", Value = "Maria" }] };
        _mockUserRepository.Setup(r => r.GetUserByIdAsync(OwnerId)).ReturnsAsync(user);

        var result = await _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId);

        var first = result.Participants.First();
        Assert.Equal(ParticipantType.Owner, first.Type);
        Assert.Equal("Maria", first.Name);
        Assert.Equal(400, result.Total);
    }

    private static Order OwnerOrder(OrderStatus status, params (string dishId, int quantity)[] dishes) => new()
    {
        OrderId = "order-1",
        CustomerId = OwnerId,
        ReservationId = ReservationId,
        ReservationAddress = "Rustaveli Avenue 48",
        ReservationTime = "Nov 1, 2026",
        Status = status,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.dishId, Quantity = d.quantity, State = OrderItemState.Pending }).ToList()
    };

    private static GuestSession EmptyOwnerSession()
    {
        var ownerSession = Session("owner-session", "Owner", false);
        ownerSession.IsOwnerSession = true;
        return ownerSession;
    }

    [Fact]
    public async Task GetGroupSessionsAsync_OwnerHasSubmittedOrder_ShowsOrderDishesAsSubmitted()
    {
        SetupOwnedGroup();
        SetupDishes();
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([Session("session1", "Andrii", true, ("dish1", 2)), EmptyOwnerSession()]);
        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync(OwnerOrder(OrderStatus.SubmittedByCustomer, ("dish2", 1)));

        var result = await _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId);

        var owner = result.Participants.First();
        Assert.Equal(ParticipantType.Owner, owner.Type);
        Assert.Equal("Tom Yum", owner.Items.Single().Dish.Name);
        Assert.Equal(220, owner.Total);
        Assert.True(owner.IsSubmitted);
        Assert.Equal(580, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_GroupSubmitted_DoesNotCountGuestDishesTwice()
    {
        SetupOwnedGroup(GroupReservationStatus.Submitted);
        SetupDishes();
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([Session("session1", "Andrii", true, ("dish1", 2)), EmptyOwnerSession()]);
        // the order already holds the owner's dish2 plus the guest's dish1 merged on submit
        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync(OwnerOrder(OrderStatus.SubmittedByCustomer, ("dish2", 1), ("dish1", 2)));

        var result = await _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId);

        Assert.Equal("Tom Yum", result.Participants.First().Items.Single().Dish.Name);
        Assert.Equal(580, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_NoOrder_OwnerStaysEmptyAndNotSubmitted()
    {
        SetupOwnedGroup();
        SetupDishes();
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([EmptyOwnerSession()]);

        var result = await _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId);

        var owner = result.Participants.Single();
        Assert.Empty(owner.Items);
        Assert.False(owner.IsSubmitted);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_NotOwner_ThrowsActionForbidden()
    {
        SetupOwnedGroup(customerId: "someone-else");

        await Assert.ThrowsAsync<ActionForbiddenException>(() => _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId));
    }

    [Fact]
    public async Task GetGroupSessionsAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.GetGroupSessionsAsync(OwnerId, GroupId));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_SessionExists_ReturnsItWithoutCreating()
    {
        SetupOwnedGroup();
        var ownerSession = Session("owner-session", "Owner", false);
        ownerSession.IsOwnerSession = true;

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([Session("session1", "Andrii", false), ownerSession]);

        var result = await _guestSessionService.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.Equal("owner-session", result.GuestSessionId);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoOwnerSession_CreatesAndReturnsIt()
    {
        SetupOwnedGroup();
        _mockGuestSessionRepository.Setup(r => r.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([]);

        GuestSession? saved = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => saved = s);

        var result = await _guestSessionService.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.NotNull(saved);
        Assert.True(saved.IsOwnerSession);
        Assert.Equal(OwnerId, saved.OwnerUserId);
        Assert.Equal(GroupId, saved.GroupReservationId);
        Assert.Equal(saved.Id, result.GuestSessionId);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ReservationNotFound_ThrowsReservationNotFound()
    {
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(ReservationId)).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _guestSessionService.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NotOwner_ThrowsActionForbidden()
    {
        SetupOwnedGroup(customerId: "someone-else");

        await Assert.ThrowsAsync<ActionForbiddenException>(() => _guestSessionService.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        SetupOwnedGroup();
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(ReservationId)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_ValidSession_DeletesAndReturnsUpdatedSnapshot()
    {
        SetupOwnedGroup();
        SetupDishes();
        var toDelete = Session("session1", "Andrii", true, ("dish1", 2));
        var remaining = Session("session2", "Olena", false, ("dish2", 1));

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId("session1")).ReturnsAsync(toDelete);
        _mockGuestSessionRepository.Setup(r => r.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([remaining]);

        var result = await _guestSessionService.DeleteGuestSessionAsync(OwnerId, GroupId, "session1");

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(toDelete), Times.Once);
        Assert.Equal("session2", result.Participants.Single().Id);
        Assert.Equal(220, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidation()
    {
        SetupOwnedGroup();
        var foreign = Session("session9", "Ivan", false);
        foreign.GroupReservationId = "other-group";
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId("session9")).ReturnsAsync(foreign);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.DeleteGuestSessionAsync(OwnerId, GroupId, "session9"));

        Assert.IsNotType<GuestSessionNotFoundException>(ex);
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionNotFound_ThrowsGuestSessionNotFound()
    {
        SetupOwnedGroup();
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId("missing")).ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _guestSessionService.DeleteGuestSessionAsync(OwnerId, GroupId, "missing"));
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_OwnerSession_ThrowsValidation()
    {
        SetupOwnedGroup();
        var ownerSession = Session("owner-session", "Owner", false);
        ownerSession.IsOwnerSession = true;
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId("owner-session")).ReturnsAsync(ownerSession);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.DeleteGuestSessionAsync(OwnerId, GroupId, "owner-session"));
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_NotOwner_ThrowsActionForbidden()
    {
        SetupOwnedGroup(customerId: "someone-else");

        await Assert.ThrowsAsync<ActionForbiddenException>(() => _guestSessionService.DeleteGuestSessionAsync(OwnerId, GroupId, "session1"));
    }
}
