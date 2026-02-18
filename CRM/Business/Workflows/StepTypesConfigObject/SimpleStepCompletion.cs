
namespace CRM.Business.Workflows.StepTypesConfigObject
{
    public class SimpleStepCompletion : StepType_ConfigObjectBase
    {
        public SimpleStepCompletion() 
        {
            TaskCompleteButtonLabel = "Complete";
            EstimatedDaysToComplete = 0;   /* 0: No time estimate. */
            UserInstructionalMessage = "Mark this task once complete.";
        }

        public enum UserResponse
        {
            Complete = 1
        }

        public string UserInstructionalMessage { get; set; }
        public int EstimatedDaysToComplete;
        public string TaskCompleteButtonLabel { get; set; }
    }
}
