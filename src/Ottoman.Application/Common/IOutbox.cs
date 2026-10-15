namespace Ottoman.Application.Common;

/// <summary>
/// Transactional outbox. Enqueue writes to the same DbContext as the business change and is
/// persisted by that unit's SaveChanges, so an event exists if and only if the change committed.
/// </summary>
public interface IOutbox
{
    void Enqueue<TEvent>(TEvent @event) where TEvent : notnull;
}
