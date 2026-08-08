using System.Reflection;
using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Application;
using S4HERP.Security.Application;
using S4HERP.Workflow.Application;
using S4HERP.Workflow.Contracts;

namespace S4HERP.Host.Infrastructure;

public static class ModuleRegistration
{
    /// <summary>
    /// Every assembly carrying <c>IEntityTypeConfiguration</c> types, in one place.
    ///
    /// It has to be one place. This list was previously copied into the design-time
    /// factory and the architecture tests as well, and adding the Workflow module to
    /// only the runtime copy scaffolded a migration that was silently *empty* — EF
    /// compared a model that had the tables against a snapshot built from a model
    /// that did not.
    /// </summary>
    public static readonly Assembly[] ModuleAssemblies =
    [
        typeof(Organization.Infrastructure.TenantConfiguration).Assembly,
        typeof(Security.Infrastructure.UserConfiguration).Assembly,
        typeof(BusinessPartner.Infrastructure.PartnerConfiguration).Assembly,
        typeof(Finance.Infrastructure.LedgerConfiguration).Assembly,
        typeof(Controlling.Infrastructure.ControllingAreaConfiguration).Assembly,
        typeof(Audit.Infrastructure.AuditLogConfiguration).Assembly,
        typeof(Workflow.Infrastructure.ApprovalRuleConfiguration).Assembly,
    ];

    /// <summary>Points <see cref="S4herpDbContext"/> at the module assemblies.</summary>
    public static void UseModuleConfigurations()
    {
        S4herpDbContext.ConfigurationAssemblies.Clear();
        S4herpDbContext.ConfigurationAssemblies.AddRange(ModuleAssemblies);
    }

    /// <summary>
    /// The composition root. Every module contributes its entity configurations
    /// and its handlers here and nowhere else, so adding or removing one is a
    /// single edit.
    /// </summary>
    public static IServiceCollection AddS4herpModules(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Set ConnectionStrings__Default.");

        UseModuleConfigurations();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SystemDbContextFactory>();
        services.AddMemoryCache();

        AddRequestContext(services, configuration, environment);

        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<S4herpDbContext>((provider, options) => options
            .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>())
            .UseSqlServer(connectionString, sql => sql
                .EnableRetryOnFailure()
                .MigrationsAssembly(typeof(ModuleRegistration).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Cfg)));

        AddDispatcherAndPipeline(services);
        AddHandlers(services);

        return services;
    }

    private static void AddRequestContext(
        IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var authentication = new AuthenticationOptions();
        configuration.GetSection("Authentication").Bind(authentication);

        // Development mode trusts a request header naming the user. Refuse to
        // start if that is switched on anywhere but Development — a misconfigured
        // environment variable must not become an authentication bypass.
        if (authentication.Mode == "Development" && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Authentication:Mode is 'Development' but the environment is " +
                $"'{environment.EnvironmentName}'. Header-based identity is a development " +
                "affordance and must never run outside Development.");
        }

        services.AddSingleton(authentication);
        services.AddScoped<RequestContext>();
        services.AddScoped<ICurrentUser>(p => p.GetRequiredService<RequestContext>());
        services.AddScoped<ITenantContext>(p => p.GetRequiredService<RequestContext>());
        services.AddScoped<IUserContext>(p => p.GetRequiredService<RequestContext>());
        services.AddScoped<ICorrelationContext>(p => p.GetRequiredService<RequestContext>());
    }

    private static void AddDispatcherAndPipeline(IServiceCollection services)
    {
        services.AddDispatcher();

        // Order matters and is part of the design (blueprint §1.3):
        // correlation → authorisation → validation → transaction → handler.
        // Authorisation before validation so a caller cannot probe field-level
        // rules on data they may not see; the transaction opened only after
        // validation so a rejected request never starts one.
        services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
        services.AddPipelineBehavior(typeof(AuthorizationBehavior<,>));
        services.AddPipelineBehavior(typeof(ValidationBehavior<,>));
        services.AddPipelineBehavior(typeof(TransactionBehavior<,>));

        services.AddScoped<IAuthorizationEnforcer, AuthorizationEnforcer>();
    }

    private static void AddHandlers(IServiceCollection services)
    {
        services.AddScoped<IFiscalPeriodService, FiscalPeriodService>();
        services.AddScoped<ICurrencyTranslator, CurrencyTranslator>();
        services.AddScoped<INumberRangeAllocator, NumberRangeAllocator>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ParkedDocumentPoster>();

        services.AddScoped<
            ICommandHandler<PostJournalEntryCommand, PostJournalEntryResult>,
            PostJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<ReverseJournalEntryCommand, ReverseJournalEntryResult>,
            ReverseJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<SubmitJournalEntryCommand, JournalWorkflowResult>,
            SubmitJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<ApproveJournalEntryCommand, JournalWorkflowResult>,
            ApproveJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<RejectJournalEntryCommand, JournalWorkflowResult>,
            RejectJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<WithdrawJournalEntryCommand, JournalWorkflowResult>,
            WithdrawJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<DeleteJournalEntryCommand, DeleteJournalEntryResult>,
            DeleteJournalEntryHandler>();
        services.AddScoped<
            ICommandHandler<PostPaymentCommand, PostPaymentResult>,
            PostPaymentHandler>();
        services.AddScoped<
            ICommandHandler<ResetClearingCommand, ResetClearingResult>,
            ResetClearingHandler>();
        services.AddScoped<
            IQueryHandler<OpenItemsQuery, OpenItemsResult>,
            OpenItemsQueryHandler>();
        services.AddScoped<
            IQueryHandler<TrialBalanceQuery, TrialBalanceResult>,
            TrialBalanceQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetJournalEntryQuery, JournalEntryDocument>,
            GetJournalEntryQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetJournalWorkflowQuery, WorkflowStateView>,
            GetJournalWorkflowQueryHandler>();
        services.AddScoped<
            IQueryHandler<MyJournalApprovalsQuery, IReadOnlyList<JournalApprovalInboxItem>>,
            MyJournalApprovalsQueryHandler>();
    }
}
