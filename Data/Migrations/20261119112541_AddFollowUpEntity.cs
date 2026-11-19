using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AcxiomCRM.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FollowUps",
                columns: table => new
                {
                    FollowUpId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    OpportunityId = table.Column<int>(type: "integer", nullable: true),
                    FollowUpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FollowUpType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUps", x => x.FollowUpId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_AssignedTo",
                table: "FollowUps",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_FollowUpDate",
                table: "FollowUps",
                column: "FollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_Status",
                table: "FollowUps",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowUps");
        }
    }
}
