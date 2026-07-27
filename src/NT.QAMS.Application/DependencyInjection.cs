using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NT.QAMS.Application.Behaviors;

namespace NT.QAMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Pipeline order is the law: tracing wraps logging wraps everything;
            // authorization decides before validation can leak request schema;
            // validation precedes every handler.
            cfg.AddOpenBehavior(typeof(TracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
