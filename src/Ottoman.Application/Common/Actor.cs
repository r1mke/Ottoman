using Ottoman.Domain.Reservations;

namespace Ottoman.Application.Common;

/// <summary>The authenticated caller. The id always comes from the token, never from a request body.</summary>
public record Actor(string UserId, ActorKind Kind)
{
    public bool IsAdmin => Kind == ActorKind.Admin;
    public bool IsEmployee => Kind == ActorKind.Employee;
    public bool IsClient => Kind == ActorKind.Client;
}
