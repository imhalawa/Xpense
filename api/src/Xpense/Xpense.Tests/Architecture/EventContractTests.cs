using FluentAssertions;
using Mono.Cecil;
using Xpense.Domain.Events;

namespace Xpense.Tests.Architecture;

[TestFixture]
public class EventContractTests
{
    private const string EntitiesNamespace = "Xpense.Domain.Entities";
    private const string EventsNamespace = "Xpense.Domain.Events";

    private static ModuleDefinition module = null!;

    [OneTimeSetUp]
    public void LoadAssembly() => module = ModuleDefinition.ReadModule(typeof(EventBody).Assembly.Location);

    [OneTimeTearDown]
    public void Dispose() => module?.Dispose();

    [Test]
    public void No_event_body_holds_an_entity()
    {
        var violations = new List<string>();

        foreach (var body in Bodies())
        {
            foreach (var property in body.Properties)
            {
                if (Mentions(property.PropertyType, EntitiesNamespace))
                    violations.Add($"{body.Name}.{property.Name} is {property.PropertyType.Name}");
            }
        }

        violations.Should().BeEmpty(
            "an event body is a wire format and must hold primitives, enums and ids only -- an entity "
            + "inside one ties unprocessed events to the current schema");
    }

    [Test]
    public void No_event_body_holds_a_value_object()
    {
        var violations = Bodies()
            .SelectMany(body => body.Properties
                .Where(property => Mentions(property.PropertyType, "Xpense.Domain.ValueObjects"))
                .Select(property => $"{body.Name}.{property.Name} is {property.PropertyType.Name}"))
            .ToList();

        violations.Should().BeEmpty(
            "amounts on the wire are minor units plus a currency, not a Money");
    }

    [Test]
    public void Every_event_body_lives_in_the_events_namespace()
    {
        var strays = Bodies()
            .Where(body => body.Namespace != EventsNamespace)
            .Select(body => body.FullName)
            .ToList();

        strays.Should().BeEmpty("event bodies belong in Xpense.Domain.Events");
    }

    private static IEnumerable<TypeDefinition> Bodies() =>
        module.GetTypes().Where(type =>
            type is { IsAbstract: false, IsInterface: false }
            && type.BaseType?.FullName == typeof(EventBody).FullName);

    private static bool Mentions(TypeReference type, string @namespace)
    {
        if (type.Namespace == @namespace)
            return true;

        return type is GenericInstanceType generic
               && generic.GenericArguments.Any(argument => Mentions(argument, @namespace));
    }
}
