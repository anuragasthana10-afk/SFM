
namespace CRM.Business.Workflows.PostStepCompletionTaskTypes
{
    public class Notifications
    {
        public Notifications() 
        {
            NotificationList = new Notification[] { };
        }

        public Notification[] NotificationList { get; set; }
    }
}
