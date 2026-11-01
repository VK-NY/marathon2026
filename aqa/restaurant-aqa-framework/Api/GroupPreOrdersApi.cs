using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupPreOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>The group pre-order routes: what the Owner calls with a sign-in token and what a guest calls with the
/// session token handed out on joining. Both travel as a bearer token.</summary>
public sealed class GroupPreOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetInviteAsync(string token) => GetAsync(Url($"/guest/invites/{token}"));

    public Task<IAPIResponse> GetStatusAsync(string groupId) => GetAsync(Url($"/guest/groups/{groupId}/status"));

    public Task<IAPIResponse> GetSessionsAsync(string groupId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupId}/sessions"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetOwnerSessionAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> SubmitGroupAsync(string groupId, string reservationId, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupId}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(new SubmitGroupRequest(reservationId)),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> RemoveSessionAsync(string groupId, string sessionId, string? bearerToken = null) =>
        DeleteAsync(Url($"/guest/groups/{groupId}/sessions/{sessionId}"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> JoinAsync(JoinGroupRequest request) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string sessionId, CartDishRequest dish, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(dish),
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> SubmitCartAsync(string sessionId, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/submit"), new APIRequestContextOptions
        {
            Headers = Headers(sessionToken),
        });
}
