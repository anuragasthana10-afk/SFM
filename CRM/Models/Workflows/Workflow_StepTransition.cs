namespace CRM.Models.Workflows
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Workflow_StepTransitions")]
    public partial class Workflow_StepTransition
    {
        public int ID { get; set; }

        public short Workflows_ID { get; set; }

        public short From_Workflow_Steps_ID { get; set; }

        public short To_Workflow_Steps_ID { get; set; }

        [StringLength(1000)]
        public string ConditionExpression { get; set; }

        [StringLength(150)]
        public string DisplayLabel { get; set; }

        public int SortOrder { get; set; }

        public bool? IsDefaultPath { get; set; }

        public bool? IsActive { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }

        public virtual Workflow Workflow { get; set; }

        public virtual Workflow_Steps From_Workflow_Step { get; set; }

        public virtual Workflow_Steps To_Workflow_Step { get; set; }
    }
}
