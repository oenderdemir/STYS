using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413121000_UpdateSeededDemoUsersPasswordToOne")]
public partial class UpdateSeededDemoUsersPasswordToOne : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [TODBase].[Users]
            SET [PasswordHash] = N'PBKDF2$100000$i0q5ESTW6OCqogjNQi+mbA==$TAC0smuvpzRkXdM7PZZzWsfrdihVch8Q69A8HXmC71E=',
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'migration_seed_group_users'
            WHERE [UserName] IN
            (
                N'tesisyoneticisi.demo',
                N'binayoneticisi.demo',
                N'resepsiyonist.demo',
                N'restoranyoneticisi.demo',
                N'garson.demo'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [TODBase].[Users]
            SET [PasswordHash] = N'PBKDF2$100000$7EIkx3zl3+g/hx5ORM0tUw==$JCeyiS0ajdez/R1BKi3K5awsF1bs+D8b2neo0E6KW+k=',
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'migration_seed_group_users'
            WHERE [UserName] IN
            (
                N'tesisyoneticisi.demo',
                N'binayoneticisi.demo',
                N'resepsiyonist.demo',
                N'restoranyoneticisi.demo',
                N'garson.demo'
            );
            """);
    }
}
