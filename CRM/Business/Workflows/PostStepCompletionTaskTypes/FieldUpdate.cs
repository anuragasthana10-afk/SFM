
namespace CRM.Business.Workflows.PostStepCompletionTaskTypes
{
    public class FieldUpdate
    {
        public FieldUpdate() 
        {
            FieldName = "";
        }

        public string FieldName { get; set; }
        public object FieldValue { get; set; }
    }
}
