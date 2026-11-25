using System.Security.Claims;

namespace B1.Api;

/// <summary>The current user id comes from the cookie authentication claims (see AuthEndpoints).</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public string Id => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Request is not authenticated");
}
