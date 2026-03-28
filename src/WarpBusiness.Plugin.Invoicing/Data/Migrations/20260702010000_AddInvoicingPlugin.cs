using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.Invoicing.Data.Migrations;

/// <inheritdoc />
public partial class AddInvoicingPlugin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "invoicing");

        migrationBuilder.CreateTable(
            name: "Invoices",
            schema: "invoicing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                CompanyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                BillingAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                ShippingAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                PaymentTerms = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                TaxAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                AmountPaid = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                BalanceDue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                DiscountPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                DiscountFixed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CustomerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                FooterText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceSettings",
            schema: "invoicing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                NextNumber = table.Column<int>(type: "integer", nullable: false),
                NumberPadding = table.Column<int>(type: "integer", nullable: false),
                DefaultPaymentTerms = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DefaultDueDays = table.Column<int>(type: "integer", nullable: false),
                DefaultTaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                DefaultCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                DefaultFooterText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                DefaultCustomerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CompanyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CompanyAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CompanyPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CompanyEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CompanyLogoUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLineItems",
            schema: "invoicing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                LineNumber = table.Column<int>(type: "integer", nullable: false),
                LineItemType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ProductSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                VariantDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                TimeEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                Hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                UnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                DiscountPercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                LineTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalSchema: "invoicing",
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InvoicePayments",
            schema: "invoicing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ReferenceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoicePayments_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalSchema: "invoicing",
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Invoice indexes
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_InvoiceNumber",
            schema: "invoicing",
            table: "Invoices",
            columns: new[] { "TenantId", "InvoiceNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_CompanyId",
            schema: "invoicing",
            table: "Invoices",
            columns: new[] { "TenantId", "CompanyId" },
            filter: "\"CompanyId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_Status",
            schema: "invoicing",
            table: "Invoices",
            columns: new[] { "TenantId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_DueDate",
            schema: "invoicing",
            table: "Invoices",
            columns: new[] { "TenantId", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_IssueDate",
            schema: "invoicing",
            table: "Invoices",
            columns: new[] { "TenantId", "IssueDate" });

        // InvoiceSettings indexes
        migrationBuilder.CreateIndex(
            name: "IX_InvoiceSettings_TenantId",
            schema: "invoicing",
            table: "InvoiceSettings",
            column: "TenantId",
            unique: true);

        // InvoiceLineItems indexes
        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_InvoiceId_LineNumber",
            schema: "invoicing",
            table: "InvoiceLineItems",
            columns: new[] { "InvoiceId", "LineNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_ProductId",
            schema: "invoicing",
            table: "InvoiceLineItems",
            column: "ProductId",
            filter: "\"ProductId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_TimeEntryId",
            schema: "invoicing",
            table: "InvoiceLineItems",
            column: "TimeEntryId",
            filter: "\"TimeEntryId\" IS NOT NULL");

        // InvoicePayments indexes
        migrationBuilder.CreateIndex(
            name: "IX_InvoicePayments_InvoiceId_PaymentDate",
            schema: "invoicing",
            table: "InvoicePayments",
            columns: new[] { "InvoiceId", "PaymentDate" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InvoiceLineItems",
            schema: "invoicing");

        migrationBuilder.DropTable(
            name: "InvoicePayments",
            schema: "invoicing");

        migrationBuilder.DropTable(
            name: "InvoiceSettings",
            schema: "invoicing");

        migrationBuilder.DropTable(
            name: "Invoices",
            schema: "invoicing");
    }
}
