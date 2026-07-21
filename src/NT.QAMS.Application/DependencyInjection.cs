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
            // Pipeline order is the law: logging wraps everything; validation
            // precedes every handler. Authorization slots in between in Phase 1.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
