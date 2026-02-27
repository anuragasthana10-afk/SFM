namespace CRM.Business.Workflows
{
    public class WorkflowStepActions
    {
        public enum ActioUIHintTypes
        {
            Primary,
            Secondary,
            Tertiary
        }

        public WorkflowStepActions() 
        {
            ActionURL = "~/api/Workflow/ProcessResponse";
            ActionLabel = "";
            ActionParameters = "";
            ActionUIHint = ActioUIHintTypes.Primary.ToString();
        }

        public string ActionURL { get; set; }
        public string ActionLabel { get; set; }
        public string ActionParameters { get; set; }
        public string ActionUIHint { get; set; }
    }
}
