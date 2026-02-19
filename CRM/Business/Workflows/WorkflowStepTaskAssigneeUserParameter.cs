namespace CRM.Business.Workflows
{
    public class WorkflowStepTaskAssigneeUserParameter
    {
        public WorkflowStepTaskAssigneeUserParameter()
        {
            IsSelected = false;
        }

        public int User_Id { get; set; }
        public string User_Name { get; set; }
        public string ActionParameter { get; set; }
        public bool IsSelected { get; set; }
    }
}
