// DomainEventDispatcher.cs - En Services/Common/
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Common;

namespace BlackJack.Services.Common;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync<T>(T domainEvent) where T : BaseDomainEvent
    {
        try
        {
            _logger.LogInformation("[EventDispatcher] Dispatching event: {EventType}", domainEvent.EventType);

            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            if (!handlers.Any())
            {
                _logger.LogWarning("[EventDispatcher] No handlers found for event type: {EventType}", domainEvent.EventType);
                return;
            }

            var tasks = handlers.Select(handler =>
                ((IDomainEventHandler<T>)handler).Handle(domainEvent));

            await Task.WhenAll(tasks);

            _logger.LogInformation("[EventDispatcher] Event {EventType} dispatched to {HandlerCount} handlers",
                domainEvent.EventType, handlers.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventDispatcher] Error dispatching event {EventType}: {Error}",
                domainEvent.EventType, ex.Message);
            // No re-throw para evitar que un handler falle interrumpa el flujo principal
        }
    }

    public async Task DispatchAsync(IEnumerable<BaseDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync((dynamic)domainEvent);
        }
    }

    public async Task DispatchEventsAsync(AggregateRoot aggregate)
    {
        if (aggregate.DomainEvents.Any())
        {
            var events = aggregate.DomainEvents.ToList();
            aggregate.ClearDomainEvents();

            await DispatchAsync(events);
        }
    }
}