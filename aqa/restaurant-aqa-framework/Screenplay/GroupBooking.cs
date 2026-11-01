namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A reservation for several people and the group built on it, as the Owner who booked it knows them.</summary>
public sealed record GroupBooking(
    string ReservationId,
    string GroupId,
    string LocationId,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerToken);

/// <summary>The waiter who serves a booking, hired for one scenario.</summary>
public sealed record WaiterAccount(string Email, string Password);

/// <summary>A guest who joined a group, with the session token the guest's own requests carry.</summary>
public sealed record GroupGuest(string Name, string SessionId, string SessionToken);

/// <summary>Who makes a request. Which token that means is decided in one place: <c>TheTokenOf</c>.</summary>
public enum Caller
{
    Owner,
    AnotherCustomer,
    Guest,
    Waiter,
    Visitor,
}
