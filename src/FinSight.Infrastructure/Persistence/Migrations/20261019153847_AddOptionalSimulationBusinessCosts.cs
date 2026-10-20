using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalSimulationBusinessCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostContext",
                table: "WhatIfSimulations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalDecisionCost",
                table: "PricingSimulations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostContext",
                table: "PricingSimulations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostContext",
                table: "WhatIfSimulations");

            migrationBuilder.DropColumn(
                name: "AdditionalDecisionCost",
                table: "PricingSimulations");

            migrationBuilder.DropColumn(
                name: "CostContext",
                table: "PricingSimulations");
        }
    }
}
