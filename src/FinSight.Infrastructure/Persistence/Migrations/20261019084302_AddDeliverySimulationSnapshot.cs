using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliverySimulationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliverySnapshot",
                table: "PricingSimulations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliverySnapshot",
                table: "PricingSimulations");
        }
    }
}
