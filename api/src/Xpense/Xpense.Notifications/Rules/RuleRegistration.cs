using Microsoft.Extensions.DependencyInjection;

namespace Xpense.Notifications.Rules;

public static class RuleRegistration
{
    public static IServiceCollection AddNotificationRules(this IServiceCollection services)
    {
        var ruleInterface = typeof(INotificationRule<>);

        var closedRuleInterfaces = typeof(RuleRegistration).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(@interface => @interface.IsGenericType
                                     && @interface.GetGenericTypeDefinition() == ruleInterface)
                .Select(@interface => (Implementation: type, Interface: @interface)))
            .ToArray();

        foreach (var (implementation, @interface) in closedRuleInterfaces)
            services.AddScoped(@interface, implementation);

        foreach (var bodyType in closedRuleInterfaces
                     .Select(rule => rule.Interface.GetGenericArguments()[0])
                     .Distinct())
        {
            services.AddScoped(
                typeof(IEventDispatcher),
                typeof(EventDispatcher<>).MakeGenericType(bodyType));
        }

        var failureHandlerInterface = typeof(IEventFailureHandler<>);
        var failureHandlers = typeof(RuleRegistration).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(@interface => @interface.IsGenericType
                                     && @interface.GetGenericTypeDefinition() == failureHandlerInterface)
                .Select(@interface => (Implementation: type, Interface: @interface)))
            .ToArray();

        foreach (var (implementation, @interface) in failureHandlers)
            services.AddScoped(@interface, implementation);

        foreach (var bodyType in failureHandlers.Select(handler => handler.Interface.GetGenericArguments()[0]).Distinct())
            services.AddScoped(typeof(IEventFailureDispatcher), typeof(EventFailureDispatcher<>).MakeGenericType(bodyType));

        return services;
    }
}
