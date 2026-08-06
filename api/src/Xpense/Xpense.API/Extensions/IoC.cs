using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xpense.API.ExceptionHandlers;
using Xpense.API.Infrastructure;
using Xpense.Persistence;

namespace Xpense.API.Extensions;

public static class IoC
{
    public static void ConfigurePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XpenseDbContext>(optionsBuilder =>
        {
            optionsBuilder.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly("Xpense.Persistence"));
        });
    }

    /// <summary>
    /// What is left after the migration to slices: one open generic. The 31 AddScoped calls
    /// that registered a class per use case are gone -- slices are discovered, not registered.
    /// </summary>
    public static void AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(OptionResolver<>));
    }

    /// <summary>
    /// Validators live nested inside their slice, so scan the whole assembly rather than
    /// naming a marker type that moves every time a feature is restructured.
    /// </summary>
    public static void AddRequestValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(IEndpoint).Assembly);
    }

    /// <summary>
    /// One handler per exception type. Order is significant: the first handler that claims an
    /// exception wins, so specific cases are registered ahead of the base type they derive
    /// from, and the catch-all goes last.
    /// </summary>
    public static void AddExceptionHandlers(this IServiceCollection services)
    {
        services.AddProblemDetails();

        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<InsufficientFundsExceptionHandler>();
        services.AddExceptionHandler<NotFoundExceptionHandler>();
        services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
        services.AddExceptionHandler<PersistenceFailedExceptionHandler>();
        services.AddExceptionHandler<FallbackExceptionHandler>();
    }

    /// <summary>
    /// One probe, and it asks the only question worth asking: can this process reach Postgres.
    /// A liveness/readiness split would be machinery for an orchestrator this project does not have.
    /// </summary>
    public static void AddHealthProbe(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<XpenseDbContext>();
    }

    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(SchemaId);

            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Xpense.API",
                    Description = "Financial Tracker and advisor",
                    // TODO: Add Terms of Use
                    Contact = new OpenApiContact
                    {
                        Name = "Mohamed Halawa",
                        Email = "imhalawa@outlook.com",
                        Url = new Uri("https://www.halawa.dev")
                    },
                });

            // Read XML Comments Generated Document
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
    }

    /// <summary>
    /// A schema id that is unique for a nested type: every name it is declared inside, outermost first,
    /// then its own. <c>CreateBudget.Request</c> becomes <c>CreateBudgetRequest</c>.
    /// </summary>
    private static string SchemaId(Type type)
    {
        var names = new List<string>();

        for (var current = type; current is not null; current = current.DeclaringType)
            names.Insert(0, current.Name);

        // Generics arrive as `Thing`1`; the arity suffix is not valid in a schema id.
        return string.Concat(names).Replace("`", string.Empty);
    }
}
