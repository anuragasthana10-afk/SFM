
namespace CRM.Business.Workflows.StepTypesConfigObject
{
    public class TimedStepCompletion : StepType_ConfigObjectBase
    {
        public TimedStepCompletion()
        {
            DurationDays = 0;
            DurationHours = 0;
            DurationMinutes = 0;
            DurationSeconds = 0;
        }

        public int DurationDays { get; set; }
        public int DurationHours { get; set; }
        public int DurationMinutes { get; set; }
        public int DurationSeconds { get; set; }
    }
}
