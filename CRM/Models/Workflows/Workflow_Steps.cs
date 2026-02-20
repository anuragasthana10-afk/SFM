namespace CRM.Models.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class Workflow_Steps
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Workflow_Steps()
        {
            Workflow_StepTransactions = new HashSet<Workflow_StepTransactions>();
            Workflow_StepTransitions_From = new HashSet<Workflow_StepTransition>();
            Workflow_StepTransitions_To = new HashSet<Workflow_StepTransition>();
        }

        public short ID { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(150)]
        public string Description { get; set; }

        public short Workflows_ID { get; set; }

        public byte Workflow_StepTypes_ID { get; set; }

        [Required]
        public string Workflow_StepTypes_ConfigData { get; set; }

        public bool? IsStartStep { get; set; }


        public bool? WaitForTriggeredWorkflowsToComplete { get; set; }

        [StringLength(150)]
        public string UserStepInstructions { get; set; }

        public int? Action_Roles_Id { get; set; }

        public int? TaskAssigner_Roles_Id { get; set; }

        [StringLength(500)]
        public string PreStepCompletion_DataValidation { get; set; }

        public string OnStepCompletion_Notifications { get; set; }

        [StringLength(500)]
        public string OnStepCompletion_FieldUpdates { get; set; }

        public string OnStepCompletion_APICalls { get; set; }

        public string OnStepReview_Notifications { get; set; }

        [StringLength(500)]
        public string OnStepReview_FieldUpdates { get; set; }

        public string OnStepReview_APICalls { get; set; }

        public string OnStepReject_Notifications { get; set; }

        [StringLength(500)]
        public string OnStepReject_FieldUpdates { get; set; }

        public string OnStepReject_APICalls { get; set; }

        public string OnStepError_Notifications { get; set; }

        [StringLength(500)]
        public string OnStepCreate_FieldUpdates { get; set; }

        public string OnStepCreate_Notifications { get; set; }

        public string OnStepCreate_APICalls { get; set; }

        public bool? IsActive { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }

        /*
        public virtual Security_Roles Security_Roles { get; set; }
        */


        public virtual Workflow_StepTypes Workflow_StepType { get; set; }

        public virtual Workflow Workflow { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Workflow_StepTransactions> Workflow_StepTransactions { get; set; }
    }
}
