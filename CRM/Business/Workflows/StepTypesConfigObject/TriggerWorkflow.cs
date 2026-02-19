
namespace CRM.Business.Workflows.StepTypesConfigObject
{
    public class TriggerWorkflow : StepType_ConfigObjectBase
    {
        public TriggerWorkflow() 
        {
            WorkflowCodes = new string[] { };
        }

        public string[] WorkflowCodes { get; set; }
    }
}
