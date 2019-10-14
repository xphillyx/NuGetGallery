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
                        VulnerablePackageId = c.String(),
                        VulnerablePackageVersionRange = c.String(),
                    })
                .PrimaryKey(t => t.Key);
            
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
            DropForeignKey("dbo.PackageVulnerabilityPackages", "Package_Key", "dbo.Packages");
            DropForeignKey("dbo.PackageVulnerabilityPackages", "PackageVulnerability_Key", "dbo.PackageVulnerabilities");
            DropIndex("dbo.PackageVulnerabilityPackages", new[] { "Package_Key" });
            DropIndex("dbo.PackageVulnerabilityPackages", new[] { "PackageVulnerability_Key" });
            DropTable("dbo.PackageVulnerabilityPackages");
            DropTable("dbo.PackageVulnerabilities");
        }
    }
}
