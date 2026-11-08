using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ottoman.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Common.BosnianValidation.Configure();
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>(ServiceLifetime.Singleton);
        return services;
    }
}

public sealed class AssemblyMarker;
