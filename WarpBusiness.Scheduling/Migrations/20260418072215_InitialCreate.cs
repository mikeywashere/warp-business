using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Scheduling.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "BreakRules",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinShiftMinutesToTrigger = table.Column<int>(type: "integer", nullable: false),
                    BreakDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    FrequencyMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxConsecutiveMinutesWithoutBreak = table.Column<int>(type: "integer", nullable: true),
                    MustStartAfterShiftMinutes = table.Column<int>(type: "integer", nullable: true),
                    MustStartBeforeShiftMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsWaivable = table.Column<bool>(type: "boolean", nullable: false),
                    CountsAsHoursWorked = table.Column<bool>(type: "boolean", nullable: false),
                    AdditionalBreakAfterMinutes = table.Column<int>(type: "integer", nullable: true),
                    OvertimeExtraBreakAfterMinutes = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkLocations",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_WorkLocations_WorkLocationId",
                        column: x => x.WorkLocationId,
                        principalSchema: "scheduling",
                        principalTable: "WorkLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTemplates",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplates_WorkLocations_WorkLocationId",
                        column: x => x.WorkLocationId,
                        principalSchema: "scheduling",
                        principalTable: "WorkLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleShifts",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ScheduledEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ActualStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ActualEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleShifts_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "scheduling",
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffingBlocks",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffingBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffingBlocks_ScheduleTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "scheduling",
                        principalTable: "ScheduleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBreaks",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ScheduledEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ActualStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ActualEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WasTaken = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBreaks_ScheduleShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "scheduling",
                        principalTable: "ScheduleShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreakRules_State_RuleType",
                schema: "scheduling",
                table: "BreakRules",
                columns: new[] { "State", "RuleType" });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Name_TenantId",
                schema: "scheduling",
                table: "Positions",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId",
                schema: "scheduling",
                table: "Positions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBreaks_ShiftId",
                schema: "scheduling",
                table: "ScheduleBreaks",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_TenantId",
                schema: "scheduling",
                table: "Schedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_WorkLocationId_StartDate_EndDate",
                schema: "scheduling",
                table: "Schedules",
                columns: new[] { "WorkLocationId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleShifts_EmployeeId",
                schema: "scheduling",
                table: "ScheduleShifts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleShifts_ScheduleId_Date",
                schema: "scheduling",
                table: "ScheduleShifts",
                columns: new[] { "ScheduleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_Name_TenantId",
                schema: "scheduling",
                table: "ScheduleTemplates",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_TenantId",
                schema: "scheduling",
                table: "ScheduleTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_WorkLocationId",
                schema: "scheduling",
                table: "ScheduleTemplates",
                column: "WorkLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffingBlocks_TemplateId_PositionId_DayOfWeek_StartTime",
                schema: "scheduling",
                table: "StaffingBlocks",
                columns: new[] { "TemplateId", "PositionId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkLocations_Name_TenantId",
                schema: "scheduling",
                table: "WorkLocations",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkLocations_TenantId",
                schema: "scheduling",
                table: "WorkLocations",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreakRules",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ScheduleBreaks",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "StaffingBlocks",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ScheduleShifts",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ScheduleTemplates",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Schedules",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "WorkLocations",
                schema: "scheduling");
        }
    }
}
