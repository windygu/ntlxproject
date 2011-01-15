﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using ASC.Common.Data;
using ASC.Common.Data.Sql;
using ASC.Common.Data.Sql.Expressions;
using ASC.Core.Tenants;
using ASC.Security.Cryptography;

namespace ASC.Core.Configuration.DAO
{
    public class TenantDAO : ITenantDAO
    {
        private string dbId = "tenant";

        private string[] columns = new[] { "ID", "Alias", "Language", "Timezone", "Name", "Tagline", "Description", "Keywords", "Country", "Address", "TrustedDomains", "TrustedDomainsEnabled", "OwnerId", "CreationDateTime", "MappedDomain", "Status", "StatusChanged" };

        private string table = "tenants_tenants";

        /// <inheritdoc/>
        public TenantDAO(string dbId)
        {
            this.dbId = dbId;
        }

        /// <inheritdoc/>
        public List<Tenant> GetTenants()
        {
            using (var dbManager = new DbManager(dbId))
            {
                return dbManager.ExecuteList<Tenant>(GetTenantQuery(Exp.Empty), ToTenant);
            }
        }

        /// <inheritdoc/>
        public Tenant GetTenant(int tenantId)
        {
            using (var dbManager = new DbManager(dbId))
            {
                var tenants = dbManager.ExecuteList<Tenant>(GetTenantQuery(Exp.Eq("ID", tenantId)), ToTenant);
                return tenants.Count == 1 ? tenants[0] : null;
            }
        }

        /// <inheritdoc/>
        public List<Tenant> FindTenants(string login, string password)
        {
            if (string.IsNullOrEmpty(login)) throw new ArgumentNullException("login");

            using (var dbManager = new DbManager(dbId))
            {
                var inQuery = new SqlQuery()
                    .From("core_user u")
                    .From("core_usersecurity s")
                    .Select("u.Tenant")
                    .Where(Exp.EqColumns("u.ID", "s.UserID"))
                    .Where("Upper(u.Email)", login.ToUpperInvariant());
                if (password != null) inQuery.Where("PwdHash", Hasher.Base64Hash(password, HashAlg.SHA256));
                return dbManager.ExecuteList<Tenant>(GetTenantQuery(Exp.In("ID", inQuery)), ToTenant);
            }
        }

        /// <inheritdoc/>
        public void CheckTenantAddress(string address)
        {
            using (var dbManager = new DbManager(dbId))
            {
                CheckTenantAlias(dbManager, address);
            }
        }

        /// <inheritdoc/>
        public Tenant GetTenant(string domain)
        {
            if (string.IsNullOrEmpty(domain)) throw new ArgumentNullException("domain");

            using (var dbManager = new DbManager(dbId))
            {
                var tenants = dbManager.ExecuteList<Tenant>(GetTenantQuery(Exp.Eq("Alias", domain.ToLowerInvariant())), ToTenant);
                return tenants.Count == 1 ? tenants[0] : null;
            }
        }

        /// <inheritdoc/>
        public Tenant SaveTenant(Tenant t)
        {
            if (t == null) throw new ArgumentNullException("tenant");

            using (var dbManager = new DbManager(dbId))
            {
                if (t.TenantId == -1)
                {
                    CheckTenantAlias(dbManager, t.TenantAlias);
                    t.TenantId = dbManager.ExecuteScalar<int>(
                        new SqlInsert(table)
                        .InColumns(columns)
                        .Values(
                            t.TenantId,
                            t.TenantAlias,
                            t.Language,
                            t.TimeZone.Id,
                            t.Name ?? t.TenantAlias,
                            t.Tagline,
                            t.Description,
                            t.Keywords,
                            t.Country,
                            t.Address,
                            t.GetTrustedDomains(),
                            t.TrustedDomainsEnabled,
                            t.OwnerId.ToString(),
                            t.CreatedDateTime,
                            t.MappedDomain,
                            t.Status,
                            t.StatusChangeDate)
                        .Identity<int>(0, -1, true)
                    );
                }
                else
                {
                    if (t.TenantId != 0)
                    {
                        ValidateTenantAlias(dbManager, t.TenantAlias);
                    }
                    dbManager.ExecuteNonQuery(
                        new SqlUpdate(table)
                        .Set(columns[1], t.TenantAlias)
                        .Set(columns[2], t.Language)
                        .Set(columns[3], t.TimeZone.Id)
                        .Set(columns[4], t.Name ?? t.TenantAlias)
                        .Set(columns[5], t.Tagline)
                        .Set(columns[6], t.Description)
                        .Set(columns[7], t.Keywords)
                        .Set(columns[8], t.Country)
                        .Set(columns[9], t.Address)
                        .Set(columns[10], t.GetTrustedDomains())
                        .Set(columns[11], t.TrustedDomainsEnabled)
                        .Set(columns[12], t.OwnerId.ToString())
                        .Set(columns[14], t.MappedDomain)
                        .Set(columns[15], t.Status)
                        .Set(columns[16], t.StatusChangeDate)
                        .Where(columns[0], t.TenantId)
                    );
                }
                SetTenantDomain(t);
            }
            return t;
        }

        /// <inheritdoc/>
        public void RemoveTenant(int tenantId)
        {
            using (var dbManager = new DbManager(dbId))
            {
                dbManager.ExecuteNonQuery(new SqlDelete(table).Where("ID", tenantId));
            }
        }


        /// <inheritdoc/>
        public TenantQuota GetTenantQuota(int tenant, string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            using (var dbManager = new DbManager(dbId))
            {
                var quotasString = dbManager
                    .ExecuteScalar<string>(
                        new SqlQuery("tenants_quota")
                        .Select("quota")
                        .Where("name", name)
                        .Where(Exp.Eq("tenant", tenant) | Exp.Eq("tenant", -1))
                        .OrderBy("tenant", false)
                        .SetMaxResults(1)
                    );
                return !string.IsNullOrEmpty(quotasString) ? new TenantQuota(quotasString) : null;
            }
        }

        /// <inheritdoc/>
        public void SetTenantQuota(int tenant, string name, TenantQuota quota)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            using (var dbManager = new DbManager(dbId))
            {
                if (quota != null)
                {
                    dbManager.ExecuteNonQuery(
                        new SqlInsert("tenants_quota", true)
                        .InColumnValue("tenant", tenant)
                        .InColumnValue("name", name)
                        .InColumnValue("quota", quota.QuotasString)
                    );
                }
                else
                {
                    dbManager.ExecuteNonQuery(
                        new SqlDelete("tenants_quota")
                        .Where("name", name)
                        .Where("tenant", tenant)
                    );
                }
            }
        }

        /// <inheritdoc/>
        public void SetTenantQuotaRow(int tenant, string name, TenantQuotaRow row, bool exchange)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (row == null) throw new ArgumentNullException("row");

            using (var dbManager = new DbManager(dbId))
            using (var tx = dbManager.BeginTransaction())
            {
                var counter = dbManager.ExecuteScalar<long>(
                    new SqlQuery("tenants_quotarow")
                    .Select("counter")
                    .Where("tenant", tenant)
                    .Where("name", name)
                    .Where("path", row.Path)
                    .Where("datetime", row.DateTime)
                );

                dbManager.ExecuteNonQuery(
                    new SqlInsert("tenants_quotarow", true)
                    .InColumnValue("tenant", tenant)
                    .InColumnValue("name", name)
                    .InColumnValue("path", row.Path)
                    .InColumnValue("datetime", row.DateTime)
                    .InColumnValue("counter", exchange ? counter + row.Counter : row.Counter)
                    .InColumnValue("tag", row.Tag)
                );

                tx.Commit();
            }
        }

        /// <inheritdoc/>
        public List<TenantQuotaRow> FindTenantQuotaRows(int tenant, string name, TenantQuotaRowQuery query)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            var sqlQuery = new SqlQuery("tenants_quotarow").Select("path", "datetime", "counter", "tag");
            if (tenant != Tenant.DEFAULT_TENANT) sqlQuery.Where("tenant", tenant);
            if (query != TenantQuotaRowQuery.All)
            {
                if (query.ByPath) sqlQuery.Where("path", query.Path);
                if (query.ByDateTime) sqlQuery.Where(Exp.Between("datetime", query.StartDateTime, query.EndDateTime));
                if (query.ByTag) sqlQuery.Where("tag", query.Tag);
            }

            using (var dbManager = new DbManager(dbId))
            {
                return dbManager
                    .ExecuteList(sqlQuery)
                    .MapToObject(
                        r =>
                        {
                            return new TenantQuotaRow((string)r[0], Convert.ToInt64(r[2]), (string)r[3]) { DateTime = Convert.ToDateTime(r[1]) };
                        }
                    );
            }
        }


        /// <inheritdoc/>
        public List<TenantOwner> GetTenantOwners()
        {
            using (var dbManager = new DbManager(dbId))
            {
                return dbManager.ExecuteList(
                    new SqlQuery("tenants_owner").Select("Email", "FirstName", "LastName", "Id")
                )
                .ConvertAll<TenantOwner>(ToTenantOwner);
            }
        }

        /// <inheritdoc/>
        public TenantOwner GetTenantOwner(Guid ownerId)
        {
            using (var dbManager = new DbManager(dbId))
            {
                return dbManager.ExecuteList(
                    new SqlQuery("tenants_owner").Select("Email", "FirstName", "LastName", "Id").Where("Id", ownerId.ToString())
                )
                .ConvertAll<TenantOwner>(ToTenantOwner)
                .Find(o => o.Id == ownerId);
            }
        }

        /// <inheritdoc/>
        public void SaveTenantOwner(TenantOwner owner)
        {
            using (var dbManager = new DbManager(dbId))
            {
                dbManager.ExecuteNonQuery(
                    new SqlInsert("tenants_owner", true)
                    .InColumns("Id", "FirstName", "LastName", "Email")
                    .Values(owner.Id.ToString(), owner.FirstName, owner.LastName, owner.Email)
                );
            }
        }


        /// <inheritdoc/>
        public string SaveTenantInterim(TenantRegistrationInfo registrationInfo)
        {
            using (var dbManager = new DbManager(dbId))
            using (var tx = dbManager.BeginTransaction())
            {
                ClearOldInterimTenants(dbManager);
                CheckTenantAlias(dbManager, registrationInfo.Address);

                var address = registrationInfo.Address.ToLowerInvariant();
                var count = dbManager.ExecuteScalar<long>(new SqlQuery("tenants_interim").SelectCount().Where("Address", address));
                if (0L < count) throw new Exception(string.Format("Address '{0}' busy.", registrationInfo.Address));

                var serializedInfo = string.Empty;
                using (var writer = new StringWriter())
                {
                    new XmlSerializer(typeof(TenantRegistrationInfo)).Serialize(writer, registrationInfo, new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
                    serializedInfo = writer.ToString();
                }
                var tenantInterimKey = Guid.NewGuid().ToString("N").ToUpper();

                dbManager.ExecuteNonQuery(
                    new SqlInsert("tenants_interim")
                    .InColumnValue("Address", address)
                    .InColumnValue("InterimKey", tenantInterimKey)
                    .InColumnValue("DropDateTime", DateTime.UtcNow.AddMinutes(2))
                    .InColumnValue("Info", serializedInfo)
                );

                tx.Commit();

                return tenantInterimKey;
            }
        }

        /// <inheritdoc/>
        public TenantRegistrationInfo GetTenantInterim(string tenantInterimKey)
        {
            if (string.IsNullOrEmpty(tenantInterimKey)) throw new ArgumentNullException("tenantInterimKey");

            using (var dbManager = new DbManager(dbId))
            {
                ClearOldInterimTenants(dbManager);
                var serializedInfo = dbManager.ExecuteScalar<string>(new SqlQuery("tenants_interim").Select("Info").Where("InterimKey", tenantInterimKey));
                dbManager.ExecuteNonQuery(new SqlDelete("tenants_interim").Where("InterimKey", tenantInterimKey));

                if (string.IsNullOrEmpty(serializedInfo)) return null;
                using (var reader = new StringReader(serializedInfo))
                {
                    return (TenantRegistrationInfo)new XmlSerializer(typeof(TenantRegistrationInfo)).Deserialize(reader);
                }
            }
        }


        /// <inheritdoc/>
        public void InitializeTemplateData(int tenant, Guid user, string[] sqlInstructions)
        {
            using (var dbManager = new DbManager(dbId))
            using (var tx = dbManager.BeginTransaction())
            {
                dbManager.ExecuteNonQuery(
                    new SqlInsert("core_acl")
                    .InColumns("Ace", "Subject", "Action", "AceType", "Object", "Tenant")
                    .Values(new SqlQuery("tenants_template_acl").Select("Ace", "Subject", "Action", "AceType", "Object", tenant.ToString()))
                );

                dbManager.ExecuteNonQuery(
                    new SqlInsert("core_subscription")
                    .InColumns("Source", "Action", "Recipient", "Object", "Unsubscribed", "Tenant")
                    .Values(new SqlQuery("tenants_template_subscription").Select("Source", "Action", "Recipient", "Object", "Unsubscribed", tenant.ToString()))
                );

                dbManager.ExecuteNonQuery(
                    new SqlInsert("core_subscriptionmethod")
                    .InColumns("Source", "Action", "Recipient", "Sender", "Tenant")
                    .Values(new SqlQuery("tenants_template_subscriptionmethod").Select("Source", "Action", "Recipient", "Sender", tenant.ToString()))
                );

                if (sqlInstructions != null && 0 < sqlInstructions.Length)
                {
                    var comand = dbManager.Connection.CreateCommand()
                        .AddParameter("tenant", tenant)
                        .AddParameter("user", user.ToString());
                    foreach (var sql in sqlInstructions)
                    {
                        comand.CommandText = sql;
                        comand.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }


        private SqlQuery GetTenantQuery(Exp where)
        {
            return new SqlQuery(table)
                .Select(columns)
                .Select(new SqlQuery(table + " t").Select("t.Alias").Where("t.ID", 0))
                .Where(where);
        }

        private Tenant ToTenant(IDataRecord row)
        {
            int id = row.Get<int>("ID");
            string alias = row.Get<string>("Alias");
            var tenant = new Tenant(id, alias)
            {
                Language = row.Get<string>("Language"),

                Name = row.Get<string>("Name"),
                Tagline = row.Get<string>("Tagline"),
                Keywords = row.Get<string>("Keywords"),
                Description = row.Get<string>("Description"),
                MappedDomain = row.Get<string>("MappedDomain"),
                Address = row.Get<string>("Address"),
                Country = row.Get<string>("Country"),

                TrustedDomainsEnabled = row.Get<bool>("TrustedDomainsEnabled"),

                CreatedDateTime = row.Get<DateTime>("CreationDateTime"),

                Status = (TenantStatus)row.Get<int>("Status"),
                StatusChangeDate = row.Get<DateTime>("StatusChanged")
            };

            tenant.SetTrustedDomains(row.Get<string>("TrustedDomains"));

            var timezoneId = row.Get<string>("Timezone");
            if (!string.IsNullOrEmpty(timezoneId))
            {
                try
                {
                    tenant.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    tenant.TimeZone = TimeZoneInfo.Utc;
                }
            }

            var ownerId = row.Get<string>("OwnerId");
            if (!string.IsNullOrEmpty(ownerId)) tenant.OwnerId = new Guid(ownerId);

            SetTenantDomain(tenant);

            return tenant;
        }

        private TenantOwner ToTenantOwner(object[] r)
        {
            return new TenantOwner((string)r[0])
            {
                FirstName = (string)r[1],
                LastName = (string)r[2],
                Id = new Guid((string)r[3])
            };
        }

        private void SetTenantDomain(Tenant tenant)
        {
            tenant.MappedDomains.Clear();
            string baseHost = ConfigurationManager.AppSettings["asc.core.tenants.base-domain"];
            if (baseHost == "localhost")
            {
                //single tenant on local host
                tenant.TenantDomain = Dns.GetHostName();
                tenant.TenantAlias = "localhost";
                tenant.MappedDomains.Add("localhost");
            }
            else
            {
                tenant.TenantDomain = tenant.TenantAlias;
                if (!string.IsNullOrEmpty(baseHost) && tenant.TenantAlias != baseHost)
                {
                    tenant.TenantDomain += "." + baseHost;
                }
                if (tenant.TenantId == 0)
                {
                    tenant.MappedDomains.Add(Dns.GetHostName().ToLowerInvariant());
                    tenant.MappedDomains.Add("localhost");
                }
            }
            tenant.MappedDomains.Add(tenant.TenantDomain.ToLowerInvariant());
            if (!string.IsNullOrEmpty(tenant.MappedDomain))
            {
                tenant.TenantDomain = tenant.MappedDomain;
                tenant.MappedDomains.Add(tenant.MappedDomain.ToLowerInvariant());
            }
            tenant.TenantDomain = tenant.TenantDomain.ToLowerInvariant();
        }


        private void ClearOldInterimTenants(DbManager dbManager)
        {
            dbManager.ExecuteNonQuery(new SqlDelete("tenants_interim").Where(Exp.Lt("DropDateTime", DateTime.UtcNow)));
        }

        /// <summary>
        /// Validates the alias and checks if it exists in the database.
        /// </summary>
        /// <param name="dbManager"></param>
        /// <param name="alias"></param>
        private void CheckTenantAlias(DbManager dbManager, string alias)
        {
            ValidateTenantAlias(dbManager, alias);

            var count = dbManager.ExecuteScalar<long>(
                new SqlQuery("tenants_tenants")
                .SelectCount()
                .Where("Alias", alias.ToLowerInvariant()));
            if (count != 0L) throw new TenantAlreadyExistsException(alias);
        }

        private void ValidateTenantAlias(DbManager dbManager, string alias)
        {
            if (string.IsNullOrEmpty(alias)) throw new TenantTooShortException("Tenat alias can not be empty.");

            alias = alias.ToLowerInvariant();
            if (alias.Length < 6 || 100 <= alias.Length) throw new TenantTooShortException("Length of address must be greater than or equal to 6 and less than or equal to 100.");

            var regex = new Regex("^[a-z0-9]([a-z0-9-]){1,98}[a-z0-9]$");
            if (!regex.IsMatch(alias)) throw new TenantIncorrectCharsException("Address contains invalid characters.");

            var count = dbManager.ExecuteScalar<long>(
                new SqlQuery("tenants_forbiden")
                .SelectCount()
                .Where("address", alias.ToLowerInvariant()));
            if (count != 0L) throw new TenantAlreadyExistsException(alias);
        }
    }
}