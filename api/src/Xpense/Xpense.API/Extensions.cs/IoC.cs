using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Reflection;
using Xpense.Persistence;
using Xpense.Persistence.Repositories;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Features.Accounts.Usecases;
using Xpense.Services.Features.Categories.UseCases;
using Xpense.Services.Features.Merchants.UseCases;
using Xpense.Services.Features.Tags.UseCases;
using Xpense.Services.Features.Transactions.UseCases;

namespace Xpense.API.Extensions.cs
{
    public static class IoC
    {
        public static void ConfigurePersistence(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services.AddDbContext<XpenseDbContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), x => x.MigrationsAssembly("Xpense.Persistence"));
            });
        }

        public static void ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(
                    "v1",
                    new OpenApiInfo()
                    {
                        Version = "v1",
                        Title = "Xpense",
                        Description = "Financial Tracking Services and Advisory",
                        // TODO: Add Terms of Use
                        Contact = new OpenApiContact()
                        {
                            Name = "Mohamed Halawa",
                            Email = "imhalawa@outlook.com",
                            Url = new Uri("https://halawa.dev/about")
                        },
                    }
                );

                // Read XML Comments Generated Document
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });
        }

        public static void ConfigureApiVersioning(this IServiceCollection services)
        {
            // Register API Versioning Services, see https://github.com/dotnet/aspnet-api-versioning/wiki
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
            });
        }

        public static void AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IMerchantRepository, MerchantRepository>();
            services.AddScoped<IPriorityRepository, PriorityRepository>();
        }

        public static void AddUseCases(this IServiceCollection services)
        {
            services.AddScoped<GetAccountByNumberUseCase>();
            services.AddScoped<GetAllAccountsUseCase>();
            services.AddScoped<CreateAccountUseCase>();
            services.AddScoped<DeleteAccountUseCase>();
            services.AddScoped<UpdateAccountUseCase>();

            services.AddScoped<CreateCategoryUseCase>();
            services.AddScoped<GetAllCategoriesUseCase>();
            services.AddScoped<GetCategoryByIdUseCase>();
            services.AddScoped<DeleteCategoryByIdUseCase>();
            services.AddScoped<UpdateCategoryUseCase>();

            services.AddScoped<CreateTagUseCase>();
            services.AddScoped<DeleteTagUseCase>();
            services.AddScoped<GetTagByIdUseCase>();
            services.AddScoped<GetAllTagsUseCase>();
            services.AddScoped<UpdateTagUseCase>();

            services.AddScoped<GetAllMerchantsUseCase>();

            services.AddScoped<DepositTransactionUseCase>();
            services.AddScoped<WithdrawTransactionUseCase>();
            services.AddScoped<GetAllTransactionsForAccountNumberUseCase>();
            services.AddScoped<GetAllTransactionsUseCase>();
        }
    }
}
