
using BlackJack.Domain.Common;

namespace BlackJack.Services.Common;

public interface IDomainEventDispatcher
{
    Task DispatchAsync<T>(T domainEvent) where T : BaseDomainEvent;
    Task DispatchAsync(IEnumerable<BaseDomainEvent> domainEvents);
    Task DispatchEventsAsync(AggregateRoot aggregate);
}