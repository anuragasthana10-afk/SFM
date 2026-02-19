namespace CRM.Models.Workflows
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class Workflow_TemplateAudit
    {
        public int ID { get; set; }

        public short Workflows_ID { get; set; }

        [StringLength(100)]
        public string ActionName { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string PayloadJson { get; set; }

        public int CreateUserID { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }
    }
}
