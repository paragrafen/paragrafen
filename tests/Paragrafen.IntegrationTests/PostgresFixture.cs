using Azure.Core;
using Npgsql;
using Paragrafen.Data;
using Testcontainers.PostgreSql;

namespace Paragrafen.IntegrationTests;

// One Postgres per test run, with the schema built by the same db/migrate.sh that runs in CI and on deploy.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg18")
        .WithResourceMapping(new DirectoryInfo(Path.Combine(RepoRoot(), "db")), "/db/")
        .WithEnvironment("PGUSER", "postgres")
        .WithEnvironment("PGDATABASE", "postgres")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var migrate = await _container.ExecAsync(["bash", "/db/migrate.sh"]);
        if (migrate.ExitCode != 0)
            throw new InvalidOperationException($"db/migrate.sh failed:\n{migrate.Stdout}\n{migrate.Stderr}");

        DataSource = ParagrafenDataSource.Create(ConnectionString, new NoCredential());
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Paragrafen.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root (Paragrafen.slnx).");
    }

    private sealed class NoCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A connection string with a password must not ask for an Entra token.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A connection string with a password must not ask for an Entra token.");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
