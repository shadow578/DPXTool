using DPXTool.DPX.Model.Constants;
using DPXTool.DPX.Model.JobInstances;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DPXTool.DPX
{
    /// <summary>
    /// Extensions for the <see cref="DPXClient"/> and related classes
    /// </summary>
    public static class DPXExtensions
    {
        /// <summary>
        /// Get the volsers that were used by this job
        /// Only valid if the job finished
        /// </summary>
        /// <param name="job">the job to get volsers of</param>
        /// <param name="onlyCompleted">only check for volsers in completed jobs</param>
        /// <param name="timeout">timeout to get job volsers, in milliseconds. if the timeout is <= 0, no timeout is used</param>
        /// <returns>a list of volsers used by this job, or null if the job was not completed</returns>
        public static async Task<string[]> GetVolsersUsed(this JobInstance job, bool onlyCompleted = true, long timeout = -1)
        {
            //check job ran to completion
            if (job.GetStatus() != JobStatus.Completed && onlyCompleted)
                return null;

            //get all logs for this job
            InstanceLogEntry[] logs = await job.GetLogEntriesAsync(getAllLogs: true, timeout: timeout);

            //search for logs with message_code "SNBJH_3332J" (volser reporting in format "xxxxL8")
            List<string> volsers = new List<string>();
            foreach (InstanceLogEntry log in logs)
                if (log.MessageCode.Equals("SNBJH_3332J", StringComparison.OrdinalIgnoreCase)//is SNBJH_3332J
                    && Regex.IsMatch(log.Message, "[0-9]{4}L[0-9]"))//check message matches volser format
                    volsers.Add(log.Message);

            return volsers.ToArray();
        }
    }
}
