using CRM.Business.Workflows;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Dynamic;

namespace CRM.Models.Workflows
{
    public partial class Workflow
    {
        public enum WorkflowCode
        {
            COMPSETUP = 1,
            COMPRENEW = 2,
            ASSOCBANKOPEN = 3,
            OTHERINVOICE = 4
        }

        [NotMapped]
        public const short m_ListRecentlyCompletedWorkflows_NumberOfDays = 14;

        public List<WorkflowStatus> GetWorkflowStatus(int iObjectRefID)
        {
            List<WorkflowStatus> workflowStatusList = new List<WorkflowStatus>();

            var activeWorkflowsList = Workflow_Transactions_Headers.Where(w => w.Workflows_ID == ID && w.Object_RefID == iObjectRefID &&  (w.DelFlag ?? false) == false).ToList();

            if(activeWorkflowsList.Where(w => !(w.WorkflowComplete ?? false)).Count() > 1)
            {
                throw new Exception("Workflow::GetWorkflowStatus() - More than one active workflow found for workflow Code: " + Code + " and Object Ref ID: " + iObjectRefID);
            }

            foreach(var workflowTransactionHeader in activeWorkflowsList.Where(w => (!(w.WorkflowComplete ?? false) || (DateTime.UtcNow - (w.WorkflowComplete_Time ?? DateTime.UtcNow)).Days <= m_ListRecentlyCompletedWorkflows_NumberOfDays)))
            {
                workflowStatusList.Add(workflowTransactionHeader.GetWorkflowStatus());
            }

            return workflowStatusList;
        }
    }
}
