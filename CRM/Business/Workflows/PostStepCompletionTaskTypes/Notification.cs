
using System.Text.Json.Serialization;

namespace CRM.Business.Workflows.PostStepCompletionTaskTypes
{
    public class Notification
    {
        public enum CRMUserExpression
        {
            AM,
            AM_Assistant,
            AM_Supervisor,
            AM_HOD,
            User,
            User_Assistant,
            User_Supervisor,
            User_HOD,
        }

        public Notification() 
        {
            NotificationName = "";
            Subject = "";
            CRMUserExpressionList = new string[] { };
            CRMUserIDList = new int[] { };
            ToEmailAddress = new string[] { };
            CcEmailAddress = new string[] { };
            BccEmailAddress = new string[] { };
            MessageBody = "";
        }

        public string NotificationName { get; set; }
        public string Subject { get; set; }

        /// <summary>
        /// AM, AM_Assisstant, AM_Supervisor, AM_HOD, User, User_Assisstant, User_Supervisor, User_HOD
        /// </summary>
        public string[] CRMUserExpressionList { get; set; }
        public int[] CRMUserIDList { get; set; }
        public string[] ToEmailAddress { get; set; }
        public string[] CcEmailAddress { get; set; }
        public string[] BccEmailAddress { get; set; }
        public string MessageBody { get; set; }
    }
}
