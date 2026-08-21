using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NiftyEdge.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NiftyEdge")
            ?? throw new InvalidOperationException("Connection string 'NiftyEdge' is not configured.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
