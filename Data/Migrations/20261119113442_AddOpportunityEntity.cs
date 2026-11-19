using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AcxiomCRM.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    OpportunityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpportunityName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.OpportunityId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_AssignedTo",
                table: "Opportunities",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_ExpectedCloseDate",
                table: "Opportunities",
                column: "ExpectedCloseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Stage",
                table: "Opportunities",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Opportunities");
        }
    }
}
