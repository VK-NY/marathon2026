namespace Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;

public sealed record InviteTokenResponse(string Token);

/// <summary>The invite endpoint answers in PascalCase; <see cref="JsonDefaults"/> reads it case-insensitively.</summary>
public sealed record GroupInviteResponse(string GroupReservationId);

public sealed record JoinGroupRequest(string GuestName, string GroupReservationId);

public sealed record GuestSessionResponse(string SessionId, string SessionToken);

public sealed record CartDishRequest(string DishId, int Quantity);

public sealed record OwnerSessionResponse(string GuestSessionId);

public sealed record SubmitGroupRequest(string ReservationId);

public sealed record AddWaiterRequest(string Email, string LocationId);

public sealed record GroupDish(string Id, string Name, decimal Price);

public sealed record GroupParticipantItem(GroupDish Dish, int Quantity);

public sealed record GroupParticipant(
    string Id,
    string Type,
    string Name,
    IReadOnlyList<GroupParticipantItem> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record GroupSnapshot(IReadOnlyList<GroupParticipant> Participants, decimal Total);
