using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using Moq;

namespace APIHandler.Tests.Services;

// Owner confirms the consolidated group pre-order.
public partial class GroupReservationServiceTests
{
    private const string SubmitOwnerId = "owner-user";
    private const string SubmitGroupId = "group-1";
    private const string SubmitReservationId = "reservation-1";

    private GroupReservation SetupSubmitGroup(
        GroupReservationStatus status = GroupReservationStatus.Open,
        string customerId = SubmitOwnerId,
        string groupReservationOf = SubmitReservationId)
    {
        var group = new GroupReservation
        {
            GroupReservationId = SubmitGroupId,
            ReservationId = groupReservationOf,
            ReservationToken = "token",
            Status = status
        };

        var reservation = new Reservation
        {
            Id = SubmitReservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = customerId,
            GuestCount = 4,
            Status = ReservationStatus.Reserved
        };

        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(SubmitGroupId)).ReturnsAsync(group);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(SubmitReservationId)).ReturnsAsync(reservation);

        return group;
    }

    private static GuestSession SubmitSession(string id, bool submitted, params (string dishId, int quantity)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = SubmitGroupId,
        GuestName = id,
        IsSubmitted = submitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.dishId, Quantity = d.quantity, State = OrderItemState.Pending }).ToList()
    };

    private static Order NewOrder(params (string dishId, int quantity)[] dishes) => new()
    {
        OrderId = "order-1",
        CustomerId = SubmitOwnerId,
        ReservationId = SubmitReservationId,
        ReservationAddress = "Some street 1, Table 1",
        ReservationTime = "Oct 30, 2026, 7:00 p.m.",
        Status = OrderStatus.Created,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.dishId, Quantity = d.quantity, State = OrderItemState.Pending }).ToList()
    };

    [Fact]
    public async Task SubmitGroupPreOrderAsync_SubmittedSessions_MergesDishesAndUpdatesStatuses()
    {
        var group = SetupSubmitGroup();

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(SubmitGroupId))
            .ReturnsAsync(
            [
                SubmitSession("s1", true, ("dish1", 2), ("dish2", 1)),
                SubmitSession("s2", true, ("dish1", 1), ("dish2", 1)),
                SubmitSession("s3", false, ("dish1", 10))
            ]);

        var order = NewOrder(("dish2", 1));
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(SubmitReservationId)).ReturnsAsync(order);

        var result = await _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Single(i => i.DishId == "dish1").Quantity);
        Assert.Equal(2, result.Single(i => i.DishId == "dish2").Quantity);

        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(3, order.Dishes.Single(d => d.DishId == "dish1").Quantity);
        Assert.Equal(3, order.Dishes.Single(d => d.DishId == "dish2").Quantity);
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(order), Times.Once);

        Assert.Equal(GroupReservationStatus.Submitted, group.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(SubmitGroupId)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_ReservationNotFound_ThrowsReservationNotFound()
    {
        SetupSubmitGroup();
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(SubmitReservationId)).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_NotOwner_ThrowsActionForbidden()
    {
        SetupSubmitGroup(customerId: "someone-else");

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_GroupOfAnotherReservation_ThrowsValidation()
    {
        SetupSubmitGroup(groupReservationOf: "other-reservation");

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_AlreadySubmitted_ThrowsGroupReservationSubmitted()
    {
        SetupSubmitGroup(status: GroupReservationStatus.Submitted);

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_NoSubmittedSessions_ThrowsValidation()
    {
        SetupSubmitGroup();
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(SubmitGroupId))
            .ReturnsAsync([SubmitSession("s1", false, ("dish1", 1))]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_SubmittedSessionsWithoutDishes_ThrowsValidation()
    {
        SetupSubmitGroup();
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(SubmitGroupId))
            .ReturnsAsync([SubmitSession("s1", true)]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_OrderNotFound_ThrowsValidationAndKeepsGroupOpen()
    {
        var group = SetupSubmitGroup();
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(SubmitGroupId))
            .ReturnsAsync([SubmitSession("s1", true, ("dish1", 1))]);
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(SubmitReservationId)).ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupPreOrderAsync(SubmitOwnerId, SubmitGroupId, SubmitReservationId));

        Assert.Equal(GroupReservationStatus.Open, group.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }
}
