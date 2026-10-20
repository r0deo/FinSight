using System.Reflection;
using FinSight.Application.Common.Behaviors;
using FinSight.Domain.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<PricingSimulationCalculator>();
        services.AddSingleton<PricingDecisionCalculator>();
        services.AddSingleton<DiscountSimulationCalculator>();
        services.AddSingleton<DeliverySimulationCalculator>();
        services.AddScoped<FinSight.Application.Features.BusinessCosts.SimulationCostContextService>();
        services.AddSingleton<WhatIfCalculator>();

        return services;
    }
}
