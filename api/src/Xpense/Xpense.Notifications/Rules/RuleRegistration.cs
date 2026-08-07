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

        return services;
    }
}
