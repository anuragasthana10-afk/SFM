
using System.Collections.Generic;

namespace CRM.Business.Workflows.Config
{
    public class Workflow_Transactions_Headers__ConfigData
    {
        public Workflow_Transactions_Headers__ConfigData()
        {
            TriggerWorkflow = new List<TriggerWorkflow_ConfigData>();
        }

        public List<TriggerWorkflow_ConfigData> TriggerWorkflow {  get; set; }
        public string AdditionalWorkflowDescription { get; set; }
    }

    public class TriggerWorkflow_ConfigData
    {
        public TriggerWorkflow_ConfigData()
        {
            TargetObject = new List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map>();
        }

        public short StepID { get; set; }
        public List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map> TargetObject { get; set;}
    }

    public class TriggerWorkflow_ConfigData_ContextObjectRefID_Map
    {
        public string WflowCode { get; set; }
        public string TgtObjCode { get; set; }
        public int ObjRefID { get; set; }
    }
}
