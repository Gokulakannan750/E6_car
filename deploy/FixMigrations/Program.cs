using System.Data.Common;
using Npgsql;

var connStr = File.ReadAllText("conn.txt").Trim();
Console.WriteLine($"Connecting...");

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("Connected.");

// Check current state
await using (var cmd = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";", conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    Console.WriteLine("\nCurrent migrations:");
    while (await reader.ReadAsync())
        Console.WriteLine($"  {reader.GetString(0)}");
}

// Insert the missing record
await using var insert = new NpgsqlCommand(
    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@m, '10.0.9') ON CONFLICT DO NOTHING;", conn);
insert.Parameters.AddWithValue("m", "20260817061158_AddIncomeAndStaffSalary");
var rows = await insert.ExecuteNonQueryAsync();
Console.WriteLine($"\nInserted migration record: {rows} row(s)");

// Verify
await using (var cmd2 = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260817061158_AddIncomeAndStaffSalary';", conn))
await using (var r2 = await cmd2.ExecuteReaderAsync())
{
    Console.WriteLine($"Migration recorded: {await r2.ReadAsync()}");
}
Console.WriteLine("Done.");
