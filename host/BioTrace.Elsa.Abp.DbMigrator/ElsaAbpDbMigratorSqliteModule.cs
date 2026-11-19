using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using BioTrace.Elsa.Abp.MultiTenancy;
using Volo.Abp.Modularity;

namespace BioTrace.Elsa.Abp;

/// <summary>
/// Demonstration DbMigrator: wires Elsa Management/Runtime to SQLite.
/// </summary>
[DependsOn(typeof(ElsaAbpMultiTenancyModule))]
public class ElsaAbpDbMigratorSqliteModule : ElsaAbpAspNetCoreModule
{
    protected override void ConfigureElsaPersistence(IModule elsa, ElsaAbpOptions options)
    {
        elsa.UseWorkflowManagement(management =>
        {
            management.UseEntityFrameworkCore(ef =>
            {
                ef.UseSqlite(ResolveElsaConnectionString);
                ef.RunMigrations = false;
            });
        });

        elsa.UseWorkflowRuntime(runtime =>
        {
            runtime.UseEntityFrameworkCore(ef =>
            {
                ef.UseSqlite(ResolveElsaConnectionString);
                ef.RunMigrations = false;
            });
        });
    }
}