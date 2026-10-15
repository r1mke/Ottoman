using System.Text.Json;
using Ottoman.Application.Common;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Outbox;

public class DbOutbox(OttomanDbContext db, TimeProvider clock) : IOutbox
{
    public void Enqueue<TEvent>(TEvent @event) where TEvent : notnull =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = typeof(TEvent).Name,
            PayloadJson = JsonSerializer.Serialize(@event),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
}
