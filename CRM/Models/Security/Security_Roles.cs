namespace CRM.Models.Security
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public partial class Security_Roles
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Security_Roles()
        {
            Security_ACL = new HashSet<Security_ACL>();
            Security_Permissions = new HashSet<Security_Permissions>();
            Security_ScreenFieldPermissions = new HashSet<Security_ScreenFieldPermissions>();
            Security_Users = new HashSet<Security_Users>();

            /*
            Workflow_Steps = new HashSet<Workflow_Steps>();
            */
        }
        /*
        public Security_Roles():base()
        {
            Security_ACL = new HashSet<Security_ACL>();
            Security_Permissions = new HashSet<Security_Permissions>();
            Security_ScreenFieldPermissions = new HashSet<Security_ScreenFieldPermissions>();
            Security_Users = new HashSet<Security_Users>();

            //Workflow_Steps = new HashSet<Workflow_Steps>();
            
            TypeDescriptor.AddProvider(new AuthorizationDescriptionProvider<Security_Roles>(TypeDescriptor.GetProvider(typeof(Security_Roles))), this);
        }

        public Security_Roles(string screencode, RBACUser user) :base(screencode, user)
        {
            Security_ACL = new HashSet<Security_ACL>();
            Security_Permissions = new HashSet<Security_Permissions>();
            Security_ScreenFieldPermissions = new HashSet<Security_ScreenFieldPermissions>();
            Security_Users = new HashSet<Security_Users>();

            //Workflow_Steps = new HashSet<Workflow_Steps>();

            TypeDescriptor.AddProvider(new AuthorizationDescriptionProvider<Security_Roles>(TypeDescriptor.GetProvider(typeof(Security_Roles))), this);
        }*/

        [Key]
        public int Role_Id { get; set; }

        [Required]
        [StringLength(100)]
        public string RoleName { get; set; }

        [StringLength(500)]
        public string RoleDescription { get; set; }

        public bool IsSysAdmin { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Security_ACL> Security_ACL { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Security_Permissions> Security_Permissions { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Security_ScreenFieldPermissions> Security_ScreenFieldPermissions { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Security_Users> Security_Users { get; set; }

        /*
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_Steps> Workflow_Steps { get; set; }
        */
    }
}
