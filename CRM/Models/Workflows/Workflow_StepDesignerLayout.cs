namespace CRM.Models.Workflows
{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Workflow_StepDesignerLayouts")]
    public partial class Workflow_StepDesignerLayout
    {
        public int ID { get; set; }

        public short Workflows_ID { get; set; }

        public short Workflow_Steps_ID { get; set; }

        public double PositionX { get; set; }

        public double PositionY { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? LastClientSyncDate { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        public int? ModifyUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ModifyDate { get; set; }

        public bool? DelFlag { get; set; }
    }
}
