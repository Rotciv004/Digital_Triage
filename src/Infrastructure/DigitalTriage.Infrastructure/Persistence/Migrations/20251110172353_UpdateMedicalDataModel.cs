using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalTriage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMedicalDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthorizedDoctorId",
                table: "MedicalDatas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MedicalDatas",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedWaitTimeMinutes",
                table: "MedicalDatas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyHistory",
                table: "MedicalDatas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncidentLocation",
                table: "MedicalDatas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfidential",
                table: "MedicalDatas",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LivingConditions",
                table: "MedicalDatas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalHistory",
                table: "MedicalDatas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreliminaryDiagnosis",
                table: "MedicalDatas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symptoms",
                table: "MedicalDatas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriageLevel",
                table: "MedicalDatas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MedicalDatas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicalFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalDataId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalFiles_MedicalDatas_MedicalDataId",
                        column: x => x.MedicalDataId,
                        principalTable: "MedicalDatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalDatas_AuthorizedDoctorId",
                table: "MedicalDatas",
                column: "AuthorizedDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalFiles_MedicalDataId",
                table: "MedicalFiles",
                column: "MedicalDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalDatas_DoctorProfiles_AuthorizedDoctorId",
                table: "MedicalDatas",
                column: "AuthorizedDoctorId",
                principalTable: "DoctorProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalDatas_DoctorProfiles_AuthorizedDoctorId",
                table: "MedicalDatas");

            migrationBuilder.DropTable(
                name: "MedicalFiles");

            migrationBuilder.DropIndex(
                name: "IX_MedicalDatas_AuthorizedDoctorId",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "AuthorizedDoctorId",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "EstimatedWaitTimeMinutes",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "FamilyHistory",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "IncidentLocation",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "IsConfidential",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "LivingConditions",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "PersonalHistory",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "PreliminaryDiagnosis",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "Symptoms",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "TriageLevel",
                table: "MedicalDatas");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MedicalDatas");
        }
    }
}
