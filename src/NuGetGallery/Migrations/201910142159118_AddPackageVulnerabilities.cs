namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPackageVulnerabilities : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PackageVulnerabilities",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        VulnerabilityKey = c.Int(nullable: false),
                        PackageId = c.String(nullable: false, maxLength: 128),
                        PackageVersionRange = c.String(nullable: false, maxLength: 132),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.Vulnerabilities", t => t.VulnerabilityKey, cascadeDelete: true)
                .Index(t => new { t.VulnerabilityKey, t.PackageId, t.PackageVersionRange }, unique: true);
            
            CreateTable(
                "dbo.Vulnerabilities",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        GitHubDatabaseKey = c.Int(nullable: false),
                        Description = c.String(),
                    })
                .PrimaryKey(t => t.Key)
                .Index(t => t.GitHubDatabaseKey, unique: true);
            
            CreateTable(
                "dbo.PackageVulnerabilityPackages",
                c => new
                    {
                        PackageVulnerability_Key = c.Int(nullable: false),
                        Package_Key = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.PackageVulnerability_Key, t.Package_Key })
                .ForeignKey("dbo.PackageVulnerabilities", t => t.PackageVulnerability_Key, cascadeDelete: true)
                .ForeignKey("dbo.Packages", t => t.Package_Key, cascadeDelete: true)
                .Index(t => t.PackageVulnerability_Key)
                .Index(t => t.Package_Key);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PackageVulnerabilities", "VulnerabilityKey", "dbo.Vulnerabilities");
            DropForeignKey("dbo.PackageVulnerabilityPackages", "Package_Key", "dbo.Packages");
            DropForeignKey("dbo.PackageVulnerabilityPackages", "PackageVulnerability_Key", "dbo.PackageVulnerabilities");
            DropIndex("dbo.PackageVulnerabilityPackages", new[] { "Package_Key" });
            DropIndex("dbo.PackageVulnerabilityPackages", new[] { "PackageVulnerability_Key" });
            DropIndex("dbo.Vulnerabilities", new[] { "GitHubDatabaseKey" });
            DropIndex("dbo.PackageVulnerabilities", new[] { "VulnerabilityKey", "PackageId", "PackageVersionRange" });
            DropTable("dbo.PackageVulnerabilityPackages");
            DropTable("dbo.Vulnerabilities");
            DropTable("dbo.PackageVulnerabilities");
        }
    }
}
