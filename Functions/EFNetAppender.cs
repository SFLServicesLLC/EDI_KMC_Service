using System;
using System.Data;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;

namespace CSCFileService
{
    public class EFNetAppender : log4net.Appender.AdoNetAppender
    {
        protected override IDbConnection CreateConnection(Type connectionType, string connectionString)
        {
            var factory = (IDbConnectionFactory)Activator.CreateInstance(connectionType);
            var builder = new EntityConnectionStringBuilder();
            var providerConnectionString = new EntityConnectionStringBuilder(connectionString).ProviderConnectionString;
            IDbConnection instance = factory.CreateConnection(providerConnectionString);
            instance.ConnectionString = providerConnectionString;
            return instance;
        }
    }
}
