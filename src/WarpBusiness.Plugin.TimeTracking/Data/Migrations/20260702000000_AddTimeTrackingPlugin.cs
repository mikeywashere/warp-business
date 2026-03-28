using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.TimeTracking.Data.Migrations;

/// <inheritdoc />
public partial class AddTimeTrackingPlugin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "timetracking");

        migrationBuilder.CreateTable(
            name: "TimeEntryTypes",
            schema: "timetracking",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeEntryTypes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EmployeePayRates",
            schema: "timetracking",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                RateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeePayRates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CustomerBillingRates",
            schema: "timetracking",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                HourlyRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerBillingRates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TimeEntries",
            schema: "timetracking",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                Hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                TimeEntryTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                CompanyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                BillingRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ApprovedById = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_TimeEntries_TimeEntryTypes_TimeEntryTypeId",
                    column: x => x.TimeEntryTypeId,
                    principalSchema: "timetracking",
                    principalTable: "TimeEntryTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntryTypes_TenantId_Name",
            schema: "timetracking",
            table: "TimeEntryTypes",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntryTypes_TenantId_DisplayOrder",
            schema: "timetracking",
            table: "TimeEntryTypes",
            columns: new[] { "TenantId", "DisplayOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeePayRates_TenantId_EmployeeId_EffectiveDate",
            schema: "timetracking",
            table: "EmployeePayRates",
            columns: new[] { "TenantId", "EmployeeId", "EffectiveDate" });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerBillingRates_TenantId_EmployeeId_CompanyId_EffectiveDate",
            schema: "timetracking",
            table: "CustomerBillingRates",
            columns: new[] { "TenantId", "EmployeeId", "CompanyId", "EffectiveDate" });

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntries_TimeEntryTypeId",
            schema: "timetracking",
            table: "TimeEntries",
            column: "TimeEntryTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntries_TenantId_EmployeeId_Date",
            schema: "timetracking",
            table: "TimeEntries",
            columns: new[] { "TenantId", "EmployeeId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntries_TenantId_CompanyId",
            schema: "timetracking",
            table: "TimeEntries",
            columns: new[] { "TenantId", "CompanyId" },
            filter: "\"CompanyId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TimeEntries_TenantId_Status",
            schema: "timetracking",
            table: "TimeEntries",
            columns: new[] { "TenantId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TimeEntries",
            schema: "timetracking");

        migrationBuilder.DropTable(
            name: "EmployeePayRates",
            schema: "timetracking");

        migrationBuilder.DropTable(
            name: "CustomerBillingRates",
            schema: "timetracking");

        migrationBuilder.DropTable(
            name: "TimeEntryTypes",
            schema: "timetracking");
    }
}
