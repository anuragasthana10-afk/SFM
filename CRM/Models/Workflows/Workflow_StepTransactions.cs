namespace CRM.Models.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Workflow_StepTransactions
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Workflow_StepTransactions()
        {
            Workflow_StepTransactionsList = new HashSet<Workflow_StepTransactions>();
        }

        public int ID { get; set; }

        public int Workflow_Transactions_Headers_ID { get; set; }

        public short Workflow_Steps_ID { get; set; }

        public bool? StepExecuted { get; set; }

        public bool? ErrorInExecution { get; set; }

        public bool? ExecutionStopped { get; set; }

        public bool? IsWaitingOnTriggeredWorkflowToComplete { get; set; }

        [StringLength(250)]
        public string SuccessMessage { get; set; }

        [StringLength(250)]
        public string ErrorMessage { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string UserComments { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string ResponseData { get; set; }

        public int? Assigned_Users_Id { get; set; }

        public int? AssignedBy_Users_Id { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? AssignedBy_Users_Time { get; set; }

        public int? ActionedBy_Users_Id { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ActionedBy_Users_Time { get; set; }

        public int? Previous_Workflow_StepTransactions_ID { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }

        public virtual Workflow_Steps Workflow_Steps { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_StepTransactions> Workflow_StepTransactionsList { get; set; }

        public virtual Workflow_StepTransactions Workflow_StepTransaction { get; set; }

        public virtual Workflow_Transactions_Headers Workflow_Transactions_Headers { get; set; }
    }
}
