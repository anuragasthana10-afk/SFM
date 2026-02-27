
namespace CRM.Business.Workflows.PostStepCompletionTaskTypes
{
    public class FieldUpdates
    {
        public FieldUpdates() 
        {
            FieldUpdateList = new FieldUpdate[] { };
        }

        public FieldUpdate[] FieldUpdateList { get; set; }
    }
}
