using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xpense.API.Infrastructure;

namespace Xpense.Tests.Architecture;

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

    [Test]
    public void No_feature_returns_forbidden_http_results()
    {
        var violations = ForbiddenCalls(module.GetTypes().Where(IsInAFeature));

        violations.Should().BeEmpty(
            "authorization failures must use the approved neutral not-found or authentication contracts, never slice-owned 403 responses");
    }

    [Test]
    public void Forbidden_result_rule_detects_its_synthetic_canary()
    {
        using var testModule = ModuleDefinition.ReadModule(typeof(SliceIsolationTests).Assembly.Location);
        var canary = testModule.GetTypes().Single(type => type.Name == nameof(ForbiddenResultCanary));

        var violations = ForbiddenCalls([canary]);
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.GenericForbidden)));
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.CallForbidden)));
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.WriteForbiddenStatus)));
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.WriteForbiddenProblem)));
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.WriteForbiddenJson)));
        violations.Should().Contain(violation => violation.Contains(nameof(ForbiddenResultCanary.WriteForbiddenLocalStatus)));
    }

    [Test]
    public void Forbidden_result_rule_detects_direct_construction_canary()
    {
        using var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("ForbiddenCanary", new Version(1, 0)),
            "ForbiddenCanary",
            ModuleKind.Dll);
        var canary = new TypeDefinition("Canary", "DirectConstruction", Mono.Cecil.TypeAttributes.Class);
        assembly.MainModule.Types.Add(canary);
        var resultType = assembly.MainModule.ImportReference(typeof(ForbidHttpResult));
        var method = new MethodDefinition(
            "ConstructForbidden",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            resultType);
        canary.Methods.Add(method);
        var constructor = typeof(ForbidHttpResult).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null)!;
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, assembly.MainModule.ImportReference(constructor)));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

        ForbiddenCalls([canary]).Should().Contain(violation => violation.Contains("ConstructForbidden"));
    }

    [Test]
    public void Forbidden_result_rule_allows_an_unrelated_403_literal()
    {
        using var testModule = ModuleDefinition.ReadModule(typeof(SliceIsolationTests).Assembly.Location);
        var canary = testModule.GetTypes().Single(type => type.Name == nameof(Harmless403Canary));

        ForbiddenCalls([canary]).Should().BeEmpty();
    }

    private static bool IsInAFeature(TypeDefinition type) => type.FullName.StartsWith(FeaturesRoot);

    private static string FeatureOf(string fullName) =>
        fullName[FeaturesRoot.Length..].Split('.', '/')[0];

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

    private static List<string> ForbiddenCalls(IEnumerable<TypeDefinition> types)
    {
        var violations = new List<string>();

        foreach (var type in types)
        {
            foreach (var field in type.Fields.Where(field => ContainsForbidHttpResult(field.FieldType)))
                violations.Add($"{type.FullName} stores {field.FieldType.FullName}");

            foreach (var method in type.Methods.Where(method => method.HasBody))
            {
                if (ReferencedForbidTypes(method).Any())
                    violations.Add($"{method.FullName} references ForbidHttpResult");

                var instructions = method.Body.Instructions;
                for (var index = 0; index < instructions.Count; index++)
                {
                    var instruction = instructions[index];
                    if (instruction.Operand is MethodReference reference
                        && reference.Name == "Forbid"
                        && reference.DeclaringType.Name is "Results" or "TypedResults")
                        violations.Add($"{method.FullName} calls {reference.FullName}");

                    if (instruction.Operand is MethodReference constructor
                        && constructor.Name == ".ctor"
                        && ContainsForbidHttpResult(constructor.DeclaringType))
                        violations.Add($"{method.FullName} constructs {constructor.DeclaringType.FullName}");

                    if (instruction.Operand is MethodReference statusWriter
                        && WritesForbiddenStatus(method, instructions, index, statusWriter))
                        violations.Add($"{method.FullName} writes an explicit 403 status");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<TypeReference> ReferencedForbidTypes(MethodDefinition method)
    {
        if (ContainsForbidHttpResult(method.ReturnType))
            yield return method.ReturnType;

        foreach (var parameter in method.Parameters.Where(parameter => ContainsForbidHttpResult(parameter.ParameterType)))
            yield return parameter.ParameterType;

        foreach (var variable in method.Body.Variables.Where(variable => ContainsForbidHttpResult(variable.VariableType)))
            yield return variable.VariableType;

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.Operand is TypeReference type && ContainsForbidHttpResult(type))
                yield return type;
            if (instruction.Operand is FieldReference field && ContainsForbidHttpResult(field.FieldType))
                yield return field.FieldType;
            if (instruction.Operand is MethodReference called)
            {
                if (ContainsForbidHttpResult(called.ReturnType))
                    yield return called.ReturnType;
                if (ContainsForbidHttpResult(called.DeclaringType))
                    yield return called.DeclaringType;
                foreach (var parameter in called.Parameters.Where(parameter => ContainsForbidHttpResult(parameter.ParameterType)))
                    yield return parameter.ParameterType;
            }
        }
    }

    private static bool ContainsForbidHttpResult(TypeReference type)
    {
        if (type.FullName == "Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult")
            return true;
        if (type is GenericInstanceType generic)
            return generic.GenericArguments.Any(ContainsForbidHttpResult);
        if (type is TypeSpecification specification)
            return ContainsForbidHttpResult(specification.ElementType);
        return false;
    }

    private static bool WritesForbiddenStatus(
        MethodDefinition containingMethod,
        Mono.Collections.Generic.Collection<Instruction> instructions,
        int callIndex,
        MethodReference called)
    {
        var isResponseSetter = called.Name == "set_StatusCode"
                               && called.DeclaringType.FullName == "Microsoft.AspNetCore.Http.HttpResponse";
        var isResultFactory = called.DeclaringType.Name is "Results" or "TypedResults";
        var parameterIndex = StatusParameterIndex(called);

        if (!isResponseSetter && (!isResultFactory || parameterIndex < 0))
            return false;

        parameterIndex = isResponseSetter ? 0 : parameterIndex;
        var valuesAbove = called.Parameters.Count - parameterIndex - 1;
        var producer = FindProducer(containingMethod, instructions, callIndex, valuesAbove);
        return producer is not null && ResolvesTo403(containingMethod, instructions, producer);
    }

    private static int StatusParameterIndex(MethodReference method)
    {
        for (var index = 0; index < method.Parameters.Count; index++)
            if (method.Parameters[index].Name == "statusCode")
                return index;

        if (method.Name == "StatusCode" && method.Parameters.Count == 1)
            return 0;
        if (method.Name == "Json" && method.Parameters.Count > 0)
            return method.Parameters.Count - 1;
        if (method.Name == "Problem")
            for (var index = 0; index < method.Parameters.Count; index++)
                if (method.Parameters[index].ParameterType.FullName == "System.Nullable`1<System.Int32>")
                    return index;
        return -1;
    }

    private static Instruction? FindProducer(
        MethodDefinition method,
        Mono.Collections.Generic.Collection<Instruction> instructions,
        int beforeIndex,
        int stackDepth)
    {
        for (var index = beforeIndex - 1; index >= 0; index--)
        {
            var instruction = instructions[index];
            var pushes = PushCount(instruction);
            if (stackDepth < pushes)
                return instruction;
            stackDepth = stackDepth - pushes + PopCount(method, instruction);
        }

        return null;
    }

    private static bool ResolvesTo403(
        MethodDefinition method,
        Mono.Collections.Generic.Collection<Instruction> instructions,
        Instruction producer)
    {
        if (producer.OpCode == OpCodes.Ldc_I4 && producer.Operand is 403)
            return true;

        var producerIndex = instructions.IndexOf(producer);
        var localIndex = LoadedLocalIndex(producer);
        if (localIndex.HasValue)
        {
            for (var index = producerIndex - 1; index >= 0; index--)
            {
                if (StoredLocalIndex(instructions[index]) != localIndex)
                    continue;
                var storedValue = FindProducer(method, instructions, index, 0);
                return storedValue is not null && ResolvesTo403(method, instructions, storedValue);
            }
        }

        if (producer.Operand is MethodReference wrapper
            && producer.OpCode == OpCodes.Newobj
            && wrapper.DeclaringType.FullName.StartsWith("System.Nullable`1<System.Int32>"))
        {
            var wrappedValue = FindProducer(method, instructions, producerIndex, wrapper.Parameters.Count - 1);
            return wrappedValue is not null && ResolvesTo403(method, instructions, wrappedValue);
        }

        return false;
    }

    private static int PushCount(Instruction instruction) => instruction.OpCode.StackBehaviourPush switch
    {
        StackBehaviour.Push0 => 0,
        StackBehaviour.Push1_push1 => 2,
        StackBehaviour.Varpush when instruction.Operand is MethodReference method =>
            instruction.OpCode == OpCodes.Newobj || method.ReturnType.MetadataType != MetadataType.Void ? 1 : 0,
        _ => 1
    };

    private static int PopCount(MethodDefinition method, Instruction instruction) => instruction.OpCode.StackBehaviourPop switch
    {
        StackBehaviour.Pop0 => 0,
        StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
        StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
            or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
            or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
        StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi
            or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4
            or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
        StackBehaviour.Varpop when instruction.Operand is MethodReference called =>
            called.Parameters.Count + (called.HasThis && instruction.OpCode != OpCodes.Newobj ? 1 : 0),
        StackBehaviour.Varpop when instruction.OpCode == OpCodes.Ret =>
            method.ReturnType.MetadataType == MetadataType.Void ? 0 : 1,
        _ => 0
    };

    private static int? LoadedLocalIndex(Instruction instruction) => instruction.OpCode.Code switch
    {
        Code.Ldloc_0 => 0,
        Code.Ldloc_1 => 1,
        Code.Ldloc_2 => 2,
        Code.Ldloc_3 => 3,
        Code.Ldloc or Code.Ldloc_S when instruction.Operand is VariableDefinition variable => variable.Index,
        _ => null
    };

    private static int? StoredLocalIndex(Instruction instruction) => instruction.OpCode.Code switch
    {
        Code.Stloc_0 => 0,
        Code.Stloc_1 => 1,
        Code.Stloc_2 => 2,
        Code.Stloc_3 => 3,
        Code.Stloc or Code.Stloc_S when instruction.Operand is VariableDefinition variable => variable.Index,
        _ => null
    };

    private static class ForbiddenResultCanary
    {
        public static Results<Ok, ForbidHttpResult> GenericForbidden() => default!;

        public static IResult CallForbidden() => TypedResults.Forbid();

        public static IResult WriteForbiddenStatus() => Results.StatusCode(403);

        public static IResult WriteForbiddenProblem() => TypedResults.Problem(statusCode: 403);

        public static IResult WriteForbiddenJson() => Results.Json(new { Value = 1 }, statusCode: 403);

        public static IResult WriteForbiddenLocalStatus()
        {
            var statusCode = 403;
            return Results.StatusCode(statusCode);
        }
    }

    private static class Harmless403Canary
    {
        public static int MeaninglessNumber() => 403;

        public static IResult HarmlessJsonData() => Results.Json(new { Value = 403 });
    }
}
