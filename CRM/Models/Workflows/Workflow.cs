namespace CRM.Models.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class Workflow
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Workflow()
        {
            Workflow_Steps = new HashSet<Workflow_Steps>();
            Workflow_Transactions_Headers = new HashSet<Workflow_Transactions_Headers>();
            Workflow_StepTransitions = new HashSet<Workflow_StepTransition>();
        }

        public short ID { get; set; }

        [Required]
        [StringLength(25)]
        public string Code { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(150)]
        public string Description { get; set; }

        [Required]
        [StringLength(15)]
        public string Context_Object_Code { get; set; }

        [StringLength(15)]
        public string Context_Screen_Code { get; set; }

        [StringLength(250)]
        public string Filters { get; set; }

        public bool? IsDefaultWorkflow_ForContextObject { get; set; }

        [StringLength(50)]
        public string TriggerConditions { get; set; }

        public short? DependsOn_Workflows_ID { get; set; }

        public bool? IsActive { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }

        /*
        public virtual Object Object { get; set; }
        */

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_Steps> Workflow_Steps { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_Transactions_Headers> Workflow_Transactions_Headers { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow> DependsOn_Workflows_List { get; set; }

        public virtual Workflow DependsOn_Workflow { get; set; }
    }
}
