using System;
using System.Collections.Generic;
using ASC.Core.Tenants;

namespace ASC.Core
{
    public interface ITenantManagerClient
    {
        List<Tenant> GetTenants();

        Tenant GetTenant(int tenantId);

        Tenant GetTenant(string domain);

        Tenant SaveTenant(Tenant tenant);

        void RemoveTenant(int tenantId);

        Tenant GetCurrentTenant();

        Tenant GetCurrentTenant(bool throwOnError);

        void SetCurrentTenant(Tenant tenant);

        void SetCurrentTenant(int tenantId);

        void SetCurrentTenant(string domain);

        TenantQuota GetTenantQuota(int tenant, string name);

        void SetTenantQuota(int tenant, string name, TenantQuota quota);

        void SetTenantQuotaRow(int tenant, string name, TenantQuotaRow row, bool exchange);

        List<TenantQuotaRow> FindTenantQuotaRows(int tenant, string name, TenantQuotaRowQuery query);

        void CheckTenantAddress(string address);
    }
}
