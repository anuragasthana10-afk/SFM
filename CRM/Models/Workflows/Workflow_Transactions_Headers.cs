namespace CRM.Models.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class Workflow_Transactions_Headers
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Workflow_Transactions_Headers()
        {
            Workflow_StepTransactions = new HashSet<Workflow_StepTransactions>();
        }

        public int ID { get; set; }

        public short Workflows_ID { get; set; }

        [Required]
        [StringLength(15)]
        public string Context_Object_Code { get; set; }

        public int Object_RefID { get; set; }

        public bool? WorkflowComplete { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? WorkflowComplete_Time { get; set; }

        public int? WorkflowTriggerSource_Workflow_StepTransactions_ID { get; set; }

        public int? DependsOn_WorkFlows_Workflow_Transactions_Headers_ID { get; set; }

        public bool? DependsOn_WorkFlows_WaitingOnCompletion { get; set; }

        [StringLength(500)]
        public string ConfigData { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }

        public virtual Object Object { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_StepTransactions> Workflow_StepTransactions { get; set; }

        public virtual Workflow Workflow { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_Transactions_Headers> Dependendent_Workflow_Transactions_Headers_List { get; set; }

        public virtual Workflow_Transactions_Headers DependsOn_Workflow_Transaction_Header { get; set; }
    }
}
