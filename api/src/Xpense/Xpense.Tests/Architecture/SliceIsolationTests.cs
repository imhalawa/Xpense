using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xpense.API.Infrastructure;

namespace Xpense.Tests.Architecture;

/// <summary>
/// The move to vertical slices gave up a compiler-enforced boundary: Xpense.Domain literally
/// could not reference the API project, so a whole class of mistake was impossible. Slice
/// isolation is only a convention, and a convention in a README is a weaker constraint than a
/// build error -- for people and for AI agents alike.
/// <para>
/// These tests put that enforcement back. See
/// docs/vertical-slicing-architecture/06-ai-assisted-development.md.
/// </para>
/// </summary>
[TestFixture]
public class SliceIsolationTests
{
    private const string FeaturesRoot = "Xpense.API.Features.";

    private static ModuleDefinition module;

    [OneTimeSetUp]
    public void LoadAssembly() =>
        module = ModuleDefinition.ReadModule(typeof(IEndpoint).Assembly.Location);

    [OneTimeTearDown]
    public void Dispose() => module?.Dispose();

    /// <summary>
    /// Contracts returned by more than one feature live in <c>Xpense.API.Contracts</c>, which is
    /// outside Features and therefore always allowed. Analytics embeds a category, so
    /// CategoryResponse cannot live inside the Categories feature without creating exactly the
    /// slice-to-slice dependency this test forbids; MoneyResponse and Timestamps are there for the
    /// same reason.
    /// </summary>
    [Test]
    public void No_slice_references_a_type_from_another_feature()
    {
        var violations = new List<string>();

        foreach (var type in module.GetTypes().Where(IsInAFeature))
        {
            var owningFeature = FeatureOf(type.FullName);

            foreach (var referenced in ReferencedTypeNames(type).Where(name => name.StartsWith(FeaturesRoot)))
            {
                var referencedFeature = FeatureOf(referenced);
                if (referencedFeature != owningFeature)
                    violations.Add($"{type.FullName} -> {referenced}");
            }
        }

        violations.Should().BeEmpty(
            "slices must not depend on each other; move anything shared into Xpense.Domain "
            + "or an explicitly shared helper");
    }

    [Test]
    public void No_slice_catches_a_domain_exception()
    {
        var violations = module.GetTypes()
            .Where(IsInAFeature)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody && method.Body.HasExceptionHandlers)
            .SelectMany(method => method.Body.ExceptionHandlers
                .Where(handler => handler.CatchType?.FullName.StartsWith("Xpense.Domain.Exceptions") == true)
                .Select(handler => $"{method.FullName} catches {handler.CatchType.FullName}"))
            .ToList();

        violations.Should().BeEmpty(
            "the ExceptionHandlers own HTTP mapping; catching in a slice puts the contract in two places");
    }

    [Test]
    public void Every_endpoint_exposes_a_public_static_Map()
    {
        var endpoints = typeof(IEndpoint).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)));

        foreach (var endpoint in endpoints)
        {
            endpoint.GetMethod("Map", BindingFlags.Public | BindingFlags.Static)
                .Should().NotBeNull("{0} implements IEndpoint, so discovery needs its Map", endpoint.FullName);
        }
    }

    [Test]
    public void Every_slice_lives_under_a_feature_folder()
    {
        var strays = typeof(IEndpoint).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Where(type => !type.FullName!.StartsWith(FeaturesRoot))
            .Select(type => type.FullName)
            .ToList();

        strays.Should().BeEmpty("endpoints belong in Xpense.API.Features.<Feature>");
    }

    private static bool IsInAFeature(TypeDefinition type) => type.FullName.StartsWith(FeaturesRoot);

    /// <summary>"Xpense.API.Features.Tags.CreateTag/Request" -> "Tags".</summary>
    private static string FeatureOf(string fullName) =>
        fullName[FeaturesRoot.Length..].Split('.', '/')[0];

    /// <summary>
    /// Every type this type mentions: base type, interfaces, field and property types, method
    /// signatures, and anything referenced from IL bodies.
    /// </summary>
    private static IEnumerable<string> ReferencedTypeNames(TypeDefinition type)
    {
        if (type.BaseType is not null) yield return type.BaseType.FullName;

        foreach (var @interface in type.Interfaces)
            yield return @interface.InterfaceType.FullName;

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
