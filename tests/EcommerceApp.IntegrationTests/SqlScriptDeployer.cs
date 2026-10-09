using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace EcommerceApp.IntegrationTests;

// Runs the /database T-SQL scripts the same way database/deploy.sh does with sqlcmd:
// GO-separated batches, $(Variable) substitution, migrations and seeds recorded in dbo.SchemaVersions.
public static partial class SqlScriptDeployer
{
    public static async Task DeployAsync(string masterConnectionString, IReadOnlyDictionary<string, string> variables, bool seed)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "database");
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await RunFileAsync(connection, Path.Combine(root, "000_create_database.sql"), variables);
        connection.ChangeDatabase(variables["DatabaseName"]);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "migrations"), "*.sql").Order()) await ApplyOnceAsync(connection, root, file, variables);
        foreach (var name in new[] { "views.sql", "procedures.sql" }) await RunFileAsync(connection, Path.Combine(root, "programmability", name), variables);
        await RunFileAsync(connection, Path.Combine(root, "security", "app_login.sql"), variables);
        connection.ChangeDatabase(variables["DatabaseName"]);
        if (seed) foreach (var file in Directory.GetFiles(Path.Combine(root, "seed"), "*.sql").Order()) await ApplyOnceAsync(connection, root, file, variables);
    }

    private static async Task ApplyOnceAsync(SqlConnection connection, string root, string file, IReadOnlyDictionary<string, string> variables)
    {
        var name = Path.GetRelativePath(root, file).Replace('\\', '/');
        await using var check = new SqlCommand("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName = @name", connection);
        check.Parameters.AddWithValue("@name", name);
        if ((int)(await check.ExecuteScalarAsync())! > 0) return;
        await RunFileAsync(connection, file, variables);
        await using var record = new SqlCommand("INSERT dbo.SchemaVersions (ScriptName) VALUES (@name)", connection);
        record.Parameters.AddWithValue("@name", name);
        await record.ExecuteNonQueryAsync();
    }

    private static async Task RunFileAsync(SqlConnection connection, string file, IReadOnlyDictionary<string, string> variables)
    {
        var sql = Variable().Replace(await File.ReadAllTextAsync(file), m => variables[m.Groups[1].Value]);
        foreach (var batch in BatchSeparator().Split(sql).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync();
        }
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex BatchSeparator();
    [GeneratedRegex(@"\$\((\w+)\)")] private static partial Regex Variable();
}
