namespace CRM.Models.Security
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Data.Entity.ModelConfiguration.Conventions;
    using System.Data.Common;
    using CRM.Classes.Security;

    public partial class RBAC_Model : CRM.Business.RBAC.RBACContext
    {
        public RBAC_Model()
            : base("name=RBAC_Model")
        {
            init();
        }

        public RBAC_Model(System.Data.Common.DbConnection existingConnection, DbTransaction existingTransaction = null)
            : base(existingConnection, existingTransaction)
        {
            init();
        }

        void init()
        {
            Security_ClientUsers = new FilteredDbSet<Security_ClientUsers>(this, c => (c.DelFlag == false || c.DelFlag.Equals(null)));
            Security_ClientUsers_Companies = new FilteredDbSet<Security_ClientUsers_Companies>(this, c => (c.DelFlag ?? false) == false);
            Security_ACL = new FilteredDbSet<Security_ACL>(this, c => (c.DelFlag == false || c.DelFlag.Equals(null)));
        }

        public virtual IDbSet<Security_ACL> Security_ACL { get; set; }
        public virtual DbSet<Security_Permissions> Security_Permissions { get; set; }
        public virtual DbSet<Security_Roles> Security_Roles { get; set; }
        public virtual DbSet<Security_ScreenFieldPermissions> Security_ScreenFieldPermissions { get; set; }
        public virtual DbSet<Security_User_LoginActivity> Security_User_LoginActivity { get; set; }
        public virtual DbSet<Security_Users> Security_Users { get; set; }
        public virtual DbSet<AuditTable> AuditTable { get; set; }
        public virtual DbSet<UserProfiles> UserProfiles { get; set; }
        public virtual IDbSet<Security_ClientUsers> Security_ClientUsers { get; set; }
        public virtual IDbSet<Security_ClientUsers_Companies> Security_ClientUsers_Companies { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //Stop doing table hash check to ignore manual changes to table definition.
            Database.SetInitializer<RBAC_Model>(null);
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Security_Permissions>()
                .HasMany(e => e.Security_Roles)
                .WithMany(e => e.Security_Permissions)
                .Map(m => m.ToTable("Security_RolePermissions").MapLeftKey("Permission_Id").MapRightKey("Role_Id"));

            modelBuilder.Entity<Security_Roles>()
                .HasMany(e => e.Security_ACL)
                .WithRequired(e => e.Security_Roles)
                .HasForeignKey(e => e.Security_Roles_Role_Id)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Security_Roles>()
                .HasMany(e => e.Security_ScreenFieldPermissions)
                .WithMany(e => e.Security_Roles)
                .Map(m => m.ToTable("Security_RoleScreenFieldPermissions").MapLeftKey("Role_Id").MapRightKey("ScreenFieldPermission_Id"));

            modelBuilder.Entity<Security_Roles>()
                .HasMany(e => e.Security_Users)
                .WithMany(e => e.Security_Roles)
                .Map(m => m.ToTable("Security_UserRoles").MapLeftKey("Role_Id").MapRightKey("User_Id"));

            modelBuilder.Entity<Security_Users>()
                .HasMany(e => e.Security_User_LoginActivity)
                .WithRequired(e => e.Security_Users)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Security_Users>()
                .HasMany(e => e.AuditTable)
                .WithRequired(e => e.Security_Users)
                .HasForeignKey(e => e.UserId);

            modelBuilder.Entity<Security_Users>()
                .HasMany(e => e.Assisted_Users)
                .WithOptional(e => e.Assistant_User)
                .HasForeignKey(e => e.Assistant_User_Id);

            modelBuilder.Entity<Security_Users>()
                .HasMany(e => e.Supervised_Users)
                .WithOptional(e => e.Supervisor_User)
                .HasForeignKey(e => e.Supervisor_User_Id);

            modelBuilder.Entity<CompanyDepartment>()
                .HasMany(e => e.Security_Users)
                .WithOptional(e => e.CompanyDepartment)
                .HasForeignKey(e => e.CompanyDepartments_ID);

            modelBuilder.Entity<CompanyLocation2>()
                .HasMany(e => e.CompanyDepartments)
                .WithRequired(e => e.CompanyLocation)
                .HasForeignKey(e => e.CompanyLocations_ID);

            modelBuilder.Entity<Security_Users>()
                .HasMany(e => e.CompanyDepartments_HOD)
                .WithOptional(e => e.HOD_User)
                .HasForeignKey(e => e.HOD_Users_Id);

            modelBuilder.Entity<UserProfiles>()
                .HasMany(e => e.Security_Users)
                .WithOptional(e => e.UserProfile)
                .HasForeignKey(e => e.UserProfiles_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AuditTable>()
                .Property(e => e.DataModel).HasMaxLength(100);
            modelBuilder.Entity<AuditTable>()
                .Property(e => e.KeyFieldID).HasMaxLength(100);

            modelBuilder.Entity<Security_ClientUsers>()
                .Property(e => e.ExternalAuthObjectID)
                .IsUnicode(false);

            modelBuilder.Entity<Security_ClientUsers>()
                .HasMany(e => e.Security_ClientUsers_Companies)
                .WithRequired(e => e.Security_ClientUsers)
                .HasForeignKey(e => e.Security_ClientUsers_ID)
                .WillCascadeOnDelete(false);
        }
    }
}
