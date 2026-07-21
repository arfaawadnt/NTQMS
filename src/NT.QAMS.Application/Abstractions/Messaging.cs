using MediatR;

namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// CQRS marker interfaces. Commands mutate exactly one aggregate and return
/// ids/refs; queries never load aggregates or trigger domain logic.
/// The markers let pipeline behaviors and architecture tests distinguish sides.
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ICommand : IRequest;

public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;

/// <summary>
/// MediatR wrapper for domain events published from the outbox. Policy handlers
/// subscribe to INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;;
/// the domain itself never references MediatR. Delivery is at-least-once —
/// every policy must be idempotent.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : SharedKernel.Primitives.IDomainEvent;
