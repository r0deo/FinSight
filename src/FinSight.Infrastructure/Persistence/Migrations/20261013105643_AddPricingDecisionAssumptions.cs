using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingDecisionAssumptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Recommendation",
                table: "PricingSimulations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProposedContribution",
                table: "PricingSimulations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "ExpectedSalesQuantity",
                table: "PricingSimulations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Difference",
                table: "PricingSimulations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "CalculationVersion",
                table: "PricingSimulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonPeriod",
                table: "PricingSimulations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencySnapshot",
                table: "PricingSimulations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeclinePercentage",
                table: "PricingSimulations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "PricingSimulations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not discard decision assumptions or turn an unknown expectation into zero.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PricingSimulations"
                        WHERE "CalculationVersion" > 0 OR "ExpectedSalesQuantity" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert pricing decision assumptions while new decision records exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "CalculationVersion",
                table: "PricingSimulations");

            migrationBuilder.DropColumn(
                name: "ComparisonPeriod",
                table: "PricingSimulations");

            migrationBuilder.DropColumn(
                name: "CurrencySnapshot",
                table: "PricingSimulations");

            migrationBuilder.DropColumn(
                name: "DeclinePercentage",
                table: "PricingSimulations");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "PricingSimulations");

            migrationBuilder.AlterColumn<string>(
                name: "Recommendation",
                table: "PricingSimulations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProposedContribution",
                table: "PricingSimulations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ExpectedSalesQuantity",
                table: "PricingSimulations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Difference",
                table: "PricingSimulations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
