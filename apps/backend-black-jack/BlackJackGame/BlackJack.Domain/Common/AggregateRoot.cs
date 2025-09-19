using BlackJack.Domain.Common;

namespace BlackJack.Domain.Common;

public abstract class AggregateRoot : BaseEntity, IAggregateRoot
{
    private readonly List<BaseDomainEvent> _domainEvents = new();

    protected AggregateRoot() : base() { }

    protected AggregateRoot(Guid id) : base(id) { }

    public IReadOnlyCollection<BaseDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(BaseDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}