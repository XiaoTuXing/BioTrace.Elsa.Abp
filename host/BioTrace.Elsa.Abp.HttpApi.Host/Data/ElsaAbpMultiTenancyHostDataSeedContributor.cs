using BioTrace.Elsa.Abp.MultiTenancy;
using BioTrace.Elsa.Abp.Permissions;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace BioTrace.Elsa.Abp.Data;

public class ElsaAbpMultiTenancyHostDataSeedContributor : ElsaAbpPermissionDataSeedContributor
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantManager _tenantManager;
    private readonly ITenantNormalizer _tenantNormalizer;

    public ElsaAbpMultiTenancyHostDataSeedContributor(
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        ITenantManager tenantManager,
        ITenantNormalizer tenantNormalizer,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        IdentityUserManager userManager,
        IPermissionManager permissionManager,
        Microsoft.AspNetCore.Identity.ILookupNormalizer lookupNormalizer,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<ElsaAbpPermissionSeedOptions> permissionSeedOptions)
        : base(
            guidGenerator,
            currentTenant,
            roleRepository,
            userRepository,
            userManager,
            permissionManager,
            lookupNormalizer,
            unitOfWorkManager,
            permissionSeedOptions)
    {
        _tenantRepository = tenantRepository;
        _tenantManager = tenantManager;
        _tenantNormalizer = tenantNormalizer;
    }

    public override async Task SeedAsync(DataSeedContext context)
    {
        await SeedTenantAsync(
            ElsaAbpMultiTenancySeedData.TenantAName,
            "tenant-a-admin@localhost",
            ElsaAbpMultiTenancySeedData.TenantAAdminUserName,
            designerUserName: ElsaAbpMultiTenancySeedData.TenantADesignerUserName,
            designerEmail: "tenant-a-designer@localhost");

        await SeedTenantAsync(
            ElsaAbpMultiTenancySeedData.TenantBName,
            "tenant-b-admin@localhost",
            ElsaAbpMultiTenancySeedData.TenantBAdminUserName,
            designerUserName: ElsaAbpMultiTenancySeedData.TenantBDesignerUserName,
            designerEmail: "tenant-b-designer@localhost");

        await EnsureTenantIdAsync(ElsaAbpMultiTenancySeedData.TenantCjkName);
    }

    protected virtual async Task SeedTenantAsync(
        string tenantName,
        string adminEmail,
        string adminUserName,
        string designerUserName,
        string designerEmail)
    {
        var tenantId = await EnsureTenantIdAsync(tenantName);
        await SeedTenantUsersAsync(tenantId, adminUserName, adminEmail, designerUserName, designerEmail);
    }

    protected virtual async Task<Guid> EnsureTenantIdAsync(string tenantName)
    {
        var normalizedName = _tenantNormalizer.NormalizeName(tenantName);

        // Keep every DB access inside this short-lived UoW: a context opened in the
        // DataSeeder ambient UoW would hold the SQLite write lock until all contributors finish.
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var tenant = await _tenantRepository.FindByNameAsync(normalizedName);
            if (tenant != null)
            {
                await uow.CompleteAsync();
                return tenant.Id;
            }

            tenant = await _tenantManager.CreateAsync(tenantName);
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            await uow.CompleteAsync();
            return tenant.Id;
        }
    }

    protected virtual async Task SeedTenantUsersAsync(
        Guid tenantId,
        string adminUserName,
        string adminEmail,
        string designerUserName,
        string designerEmail)
    {
        using (CurrentTenant.Change(tenantId))
        // SQLite allows only one write transaction per file: commit this contributor's
        // writes in its own UoW instead of the DataSeeder ambient UoW, which stays open
        // until every contributor finishes and would block later contributors.
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var adminRole = await SeedRoleAsync("admin", "Administrator");
            var designerRole = await SeedRoleAsync("designer", "Workflow Designer");

            await SeedUserAsync(adminUserName, adminEmail, "1q2w3E*", adminRole.Name);
            await SeedUserAsync(designerUserName, designerEmail, "1q2w3E*", designerRole.Name);

            await GrantRolePermissionsAsync(adminRole.Name,
                AbpElsaPermissions.NotReadOnly,
                AbpElsaPermissions.WorkflowDefinitions.Read,
                AbpElsaPermissions.WorkflowDefinitions.Write,
                AbpElsaPermissions.WorkflowDefinitions.Publish,
                AbpElsaPermissions.WorkflowDefinitions.Delete,
                AbpElsaPermissions.WorkflowInstances.Read,
                AbpElsaPermissions.WorkflowInstances.Write,
                AbpElsaPermissions.WorkflowInstances.Execute,
                AbpElsaPermissions.WorkflowInstances.Cancel,
                AbpElsaPermissions.WorkflowInstances.Delete);

            await GrantRolePermissionsAsync(designerRole.Name,
                AbpElsaPermissions.WorkflowDefinitions.Read,
                AbpElsaPermissions.WorkflowInstances.Read);

            await uow.CompleteAsync();
        }
    }
}
