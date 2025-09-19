// IDomainEventHandler.cs - En Services/Common/
using BlackJack.Domain.Common;

namespace BlackJack.Services.Common;

public interface IDomainEventHandler<in T> where T : BaseDomainEvent
{
    Task Handle(T domainEvent);
}