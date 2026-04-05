using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using STYS.Infrastructure.EntityFramework;

var services = new ServiceCollection();
services.AddDbContext<StysAppDbContext>(options => options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Dummy;Trusted_Connection=True;TrustServerCertificate=True"));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
Console.WriteLine($"HasPendingModelChanges={db.Database.HasPendingModelChanges()}");
var migrationsAssembly = db.GetService<IMigrationsAssembly>();
var differ = db.GetService<IMigrationsModelDiffer>();
var snapshot = migrationsAssembly.ModelSnapshot?.Model;
if (snapshot is null)
{
    Console.WriteLine("Snapshot model is null.");
    return;
}
var ops = differ.GetDifferences(snapshot.GetRelationalModel(), db.Model.GetRelationalModel());
Console.WriteLine($"OperationCount={ops.Count}");
foreach (var op in ops)
{
    Console.WriteLine(op.GetType().FullName);
    Console.WriteLine(op.ToString());
}
