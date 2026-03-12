using Hangfire;
using Hangfire.Storage;

namespace SendSmsCallAlerts.Jobs
{
    public class JobManager
    {
        public void DeleteAllJobs()
        {


            using (var connection = JobStorage.Current.GetConnection())
            {
                foreach (var recurringJob in connection.GetRecurringJobs())
                {
                    RecurringJob.RemoveIfExists(recurringJob.Id);
                }
            }

        }

    }
}
