using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luff.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseEmailAsUserKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                BEGIN TRANSACTION;

                UPDATE "RefreshTokens" SET "Username" = (SELECT "Email" FROM "Users" WHERE "Users"."Username" = "RefreshTokens"."Username");
                UPDATE "RecoveryCodes" SET "Username" = (SELECT "Email" FROM "Users" WHERE "Users"."Username" = "RecoveryCodes"."Username");

                CREATE TABLE "Users_new" (
                    "Email" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
                    "PasswordHash" TEXT NOT NULL,
                    "Role" TEXT NOT NULL,
                    "FirstName" TEXT NULL,
                    "LastName" TEXT NULL,
                    "TwoFactorEnabled" INTEGER NOT NULL,
                    "TwoFactorSecret" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                INSERT INTO "Users_new" ("Email", "PasswordHash", "Role", "FirstName", "LastName", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt", "UpdatedAt")
                    SELECT "Email", "PasswordHash", "Role", "FirstName", "LastName", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt", "UpdatedAt" FROM "Users";
                DROP TABLE "Users";
                ALTER TABLE "Users_new" RENAME TO "Users";

                CREATE TABLE "RefreshTokens_new" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
                    "Email" TEXT NOT NULL,
                    "FamilyId" TEXT NOT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "ConsumedAt" TEXT NULL,
                    "RevokedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_RefreshTokens_Users_Email" FOREIGN KEY ("Email") REFERENCES "Users" ("Email") ON DELETE CASCADE
                );
                INSERT INTO "RefreshTokens_new" ("Id", "Email", "FamilyId", "TokenHash", "ExpiresAt", "ConsumedAt", "RevokedAt", "CreatedAt", "UpdatedAt")
                    SELECT "Id", "Username", "FamilyId", "TokenHash", "ExpiresAt", "ConsumedAt", "RevokedAt", "CreatedAt", "UpdatedAt" FROM "RefreshTokens";
                DROP TABLE "RefreshTokens";
                ALTER TABLE "RefreshTokens_new" RENAME TO "RefreshTokens";
                CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
                CREATE INDEX "IX_RefreshTokens_FamilyId" ON "RefreshTokens" ("FamilyId");
                CREATE INDEX "IX_RefreshTokens_Email" ON "RefreshTokens" ("Email");

                CREATE TABLE "RecoveryCodes_new" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RecoveryCodes" PRIMARY KEY,
                    "Email" TEXT NOT NULL,
                    "CodeHash" TEXT NOT NULL,
                    "ConsumedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_RecoveryCodes_Users_Email" FOREIGN KEY ("Email") REFERENCES "Users" ("Email") ON DELETE CASCADE
                );
                INSERT INTO "RecoveryCodes_new" ("Id", "Email", "CodeHash", "ConsumedAt", "CreatedAt", "UpdatedAt")
                    SELECT "Id", "Username", "CodeHash", "ConsumedAt", "CreatedAt", "UpdatedAt" FROM "RecoveryCodes";
                DROP TABLE "RecoveryCodes";
                ALTER TABLE "RecoveryCodes_new" RENAME TO "RecoveryCodes";
                CREATE UNIQUE INDEX "IX_RecoveryCodes_CodeHash" ON "RecoveryCodes" ("CodeHash");
                CREATE INDEX "IX_RecoveryCodes_Email" ON "RecoveryCodes" ("Email");

                COMMIT;

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                BEGIN TRANSACTION;

                CREATE TABLE "Users_old" (
                    "Username" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
                    "PasswordHash" TEXT NOT NULL,
                    "Role" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "FirstName" TEXT NULL,
                    "LastName" TEXT NULL,
                    "TwoFactorEnabled" INTEGER NOT NULL,
                    "TwoFactorSecret" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                INSERT INTO "Users_old" ("Username", "PasswordHash", "Role", "Email", "FirstName", "LastName", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt", "UpdatedAt")
                    SELECT "Email", "PasswordHash", "Role", "Email", "FirstName", "LastName", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt", "UpdatedAt" FROM "Users";
                DROP TABLE "Users";
                ALTER TABLE "Users_old" RENAME TO "Users";
                CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

                CREATE TABLE "RefreshTokens_old" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
                    "Username" TEXT NOT NULL,
                    "FamilyId" TEXT NOT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "ConsumedAt" TEXT NULL,
                    "RevokedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_RefreshTokens_Users_Username" FOREIGN KEY ("Username") REFERENCES "Users" ("Username") ON DELETE CASCADE
                );
                INSERT INTO "RefreshTokens_old" ("Id", "Username", "FamilyId", "TokenHash", "ExpiresAt", "ConsumedAt", "RevokedAt", "CreatedAt", "UpdatedAt")
                    SELECT "Id", "Email", "FamilyId", "TokenHash", "ExpiresAt", "ConsumedAt", "RevokedAt", "CreatedAt", "UpdatedAt" FROM "RefreshTokens";
                DROP TABLE "RefreshTokens";
                ALTER TABLE "RefreshTokens_old" RENAME TO "RefreshTokens";
                CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
                CREATE INDEX "IX_RefreshTokens_FamilyId" ON "RefreshTokens" ("FamilyId");
                CREATE INDEX "IX_RefreshTokens_Username" ON "RefreshTokens" ("Username");

                CREATE TABLE "RecoveryCodes_old" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RecoveryCodes" PRIMARY KEY,
                    "Username" TEXT NOT NULL,
                    "CodeHash" TEXT NOT NULL,
                    "ConsumedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_RecoveryCodes_Users_Username" FOREIGN KEY ("Username") REFERENCES "Users" ("Username") ON DELETE CASCADE
                );
                INSERT INTO "RecoveryCodes_old" ("Id", "Username", "CodeHash", "ConsumedAt", "CreatedAt", "UpdatedAt")
                    SELECT "Id", "Email", "CodeHash", "ConsumedAt", "CreatedAt", "UpdatedAt" FROM "RecoveryCodes";
                DROP TABLE "RecoveryCodes";
                ALTER TABLE "RecoveryCodes_old" RENAME TO "RecoveryCodes";
                CREATE UNIQUE INDEX "IX_RecoveryCodes_CodeHash" ON "RecoveryCodes" ("CodeHash");
                CREATE INDEX "IX_RecoveryCodes_Username" ON "RecoveryCodes" ("Username");

                COMMIT;

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
