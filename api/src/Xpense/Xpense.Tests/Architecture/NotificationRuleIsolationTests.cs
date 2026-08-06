using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Xpense.Notifications.Rules;

namespace Xpense.Tests.Architecture;

[TestFixture]
public class NotificationRuleIsolationTests
{
    private const string RulesNamespace = "Xpense.Notifications.Rules";

    private static ModuleDefinition module = null!;

    [OneTimeSetUp]
    public void LoadAssembly() =>
        module = ModuleDefinition.ReadModule(typeof(INotificationRule<>).Assembly.Location);

    [OneTimeTearDown]
    public void Dispose() => module?.Dispose();

    [Test]
    public void No_rule_references_another_rule()
    {
        var ruleNames = Rules().Select(rule => rule.FullName).ToHashSet();
        var violations = new List<string>();

        foreach (var rule in Rules())
        {
            foreach (var referenced in ReferencedTypeNames(rule))
            {
                if (ruleNames.Contains(referenced) && referenced != rule.FullName)
                    violations.Add($"{rule.Name} -> {referenced}");
            }
        }

        violations.Should().BeEmpty(
            "each notification kind is defined in isolation; anything genuinely shared belongs in "
            + "Xpense.Domain, not in a helper reached from two rules");
    }

    [Test]
    public void Every_rule_is_discovered_by_the_registration_scan()
    {
        var declared = typeof(INotificationRule<>).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(@interface =>
                @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(INotificationRule<>)))
            .ToArray();

        declared.Should().NotBeEmpty("there is at least one rule, so a scan finding none is broken");

        foreach (var rule in declared)
        {
            rule.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Should().NotBeEmpty(
                    "{0} must have a public constructor or dependency injection cannot build it",
                    rule.Name);
        }
    }

    [Test]
    public void No_rule_payload_carries_a_timestamp()
    {
        var suspects = new[] { "At", "Time", "Timestamp", "Now" };

        var violations = module.GetTypes()
            .Where(type => type.Namespace == RulesNamespace && type.Name.Contains("Payload"))
            .SelectMany(payload => payload.Properties
                .Where(property =>
                    property.PropertyType.FullName is "System.DateTime" or "System.DateTimeOffset"
                    || (property.PropertyType.Name is "Nullable`1"
                        && suspects.Any(suspect => property.Name.EndsWith(suspect))))
                .Select(property => $"{payload.Name}.{property.Name}"))
            .ToList();

        violations.Should().BeEmpty(
            "a payload is hashed for deduplication, so it must contain only facts derived from the "
            + "event -- a timestamp makes every redelivery a new notification");
    }

    private static IEnumerable<TypeDefinition> Rules() =>
        module.GetTypes().Where(type =>
            type is { IsAbstract: false, IsInterface: false }
            && type.Interfaces.Any(@interface =>
                @interface.InterfaceType.Name.StartsWith("INotificationRule")));

    private static IEnumerable<string> ReferencedTypeNames(TypeDefinition type)
    {
        if (type.BaseType is not null) yield return type.BaseType.FullName;

        foreach (var field in type.Fields)
            yield return field.FieldType.FullName;

        foreach (var method in type.Methods)
        {
            yield return method.ReturnType.FullName;

            foreach (var parameter in method.Parameters)
                yield return parameter.ParameterType.FullName;

            if (!method.HasBody) continue;

            foreach (var instruction in method.Body.Instructions)
            {
                var name = instruction.Operand switch
                {
                    TypeReference typeRef => typeRef.FullName,
                    MethodReference methodRef => methodRef.DeclaringType.FullName,
                    FieldReference fieldRef => fieldRef.DeclaringType.FullName,
                    _ => null
                };

                if (name is not null) yield return name;
            }
        }
    }
}
