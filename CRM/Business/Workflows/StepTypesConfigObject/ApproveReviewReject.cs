
namespace CRM.Business.Workflows.StepTypesConfigObject
{
    public class ApproveReviewReject : StepType_ConfigObjectBase
    {
        public ApproveReviewReject()
        {
            UserInstructionalMessage = "Approve, Review or Reject this task / request.";
            EstimatedDaysToComplete = 0;   /* 0: No time estimate. */
            ApproveButtonLabel = "Approve";
            ReviewButtonLabel = "Review";
            RejectButtonLabel = "Reject";
        }

        public enum UserResponse
        {
            Approve = 1,
            Review = 2,
            Reject = 3
        }

        public string UserInstructionalMessage;
        public int EstimatedDaysToComplete;
        public string ApproveButtonLabel;
        public string ReviewButtonLabel;
        public string RejectButtonLabel;
    }
}
