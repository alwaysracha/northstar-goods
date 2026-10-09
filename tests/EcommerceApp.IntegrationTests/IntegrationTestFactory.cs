using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace EcommerceApp.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
    public const string Name = "SQL Server integration tests";
}

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DatabaseName = "NorthstarGoods";
    private const string AppLogin = "northstar_app";
    private static readonly string AppPassword = $"Tc!{Guid.NewGuid():N}"; // throwaway container, new password per run

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    // sa on the application database: for assertions about schema and seed data.
    public string AdminConnectionString => new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = DatabaseName }.ConnectionString;

    // The least-privilege login the application itself uses.
    public string AppConnectionString => new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = DatabaseName, UserID = AppLogin, Password = AppPassword }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting is applied when Program.cs creates its builder, so it wins over appsettings.Development.json
        // for values Program.cs reads before Build() (ConfigureAppConfiguration arrives too late for those).
        builder.UseSetting("ConnectionStrings:DefaultConnection", AppConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.StartAsync();
        // Same scripts, same order as database/deploy.sh, including the development seed.
        await SqlScriptDeployer.DeployAsync(_sqlServer.GetConnectionString(),
            new Dictionary<string, string> { ["DatabaseName"] = DatabaseName, ["AppLoginName"] = AppLogin, ["AppLoginPassword"] = AppPassword }, seed: true);
        _ = Services;
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}
