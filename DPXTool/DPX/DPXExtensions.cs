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
        /// check if a status indicates a failed job
        /// </summary>
        /// <param name="status">the status to check</param>
        /// <returns>indicates the status a failed job?</returns>
        public static bool IsFailedStatus(this JobStatus status)
        {
            switch (status)
            {
                case JobStatus.Running:
                case JobStatus.Resuming:
                case JobStatus.Completed:
                case JobStatus.Held:
                    return false;

                case JobStatus.Aborted:
                case JobStatus.Cancelled:
                case JobStatus.Cancelling:
                case JobStatus.Failed:
                case JobStatus.Suspended:
                case JobStatus.Suspending:
                case JobStatus.None:
                default://this is exhaustive switch, but compiler dont like :P
                    return true;
            }
        }

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

        /// <summary>
        /// Get backup data size information for a job
        /// Only valid if the job finished
        /// </summary>
        /// <param name="job">the job to get data size information of</param>
        /// <param name="onlyCompleted">only check for completed jobs</param>
        /// <param name="timeout">timeout to get job information, in milliseconds. if the timeout is <= 0, no timeout is used</param>
        /// <returns>information about the size of the backup job, or null if the job was not completed or nothing was found</returns>
        public static async Task<JobSizeInfo> GetBackupSizeAsync(this JobInstance job, bool onlyCompleted = true, long timeout = -1)
        {
            //check job ran to completion
            if (job.GetStatus() != JobStatus.Completed && onlyCompleted)
                return null;

            //get all logs for this job
            InstanceLogEntry[] logs = await job.GetLogEntriesAsync(getAllLogs: true, timeout: timeout);

            //prepare data object
            JobSizeInfo sizeInfo = new JobSizeInfo();

            //search logs for the following message codes:
            //SNBJH_3311J - total backup size       - "Total data backed up: 3670512 KB"
            //SNBJH_3313J - total data on tape      - "Total data on media: 3670592 KB"
            bool oneFound = false;
            foreach (InstanceLogEntry log in logs)
                if (!string.IsNullOrWhiteSpace(log.Message))
                    switch (log.MessageCode.ToLower())
                    {
                        case "snbjh_3311j":
                            //total backup size; parse and convert from KB to Bytes
                            sizeInfo.TotalDataBackedUp = ParseLong(log.Message.ToLower(), @"total data backed up: (\d*) kb").GetValueOrDefault(0) * 1000;
                            oneFound = true;
                            break;
                        case "snbjh_3313j":
                            //total data on tape; parse and convert from KB to Bytes
                            sizeInfo.TotalDataOnMedia = ParseLong(log.Message.ToLower(), @"total data on media: (\d*) kb").GetValueOrDefault(0) * 1000;
                            oneFound = true;
                            break;
                        default:
                            //unknown / unrelevant message
                            break;
                    }

            //return null if no matching log was found
            if (!oneFound)
                return null;

            return sizeInfo;
        }

        /// <summary>
        /// parse a long from a string using regex
        /// </summary>
        /// <param name="str">the string to parse from</param>
        /// <param name="pattern">the regex pattern to use. target long is in a capture group</param>
        /// <param name="targetCaptureGroup">the capture group to parse to long from</param>
        /// <returns>the parsed long, or null if parse failed</returns>
        static long? ParseLong(string str, string pattern, int targetCaptureGroup = 1)
        {
            //run regex with the given pattern on input string
            Match m = Regex.Match(str, pattern);

            // check we have a successfull match and target capture group is in bounds
            if (!m.Success
                || targetCaptureGroup < 0
                || targetCaptureGroup >= m.Groups.Count
                || !m.Groups[targetCaptureGroup].Success)
                return null;

            //get target capture group
            string target = m.Groups[targetCaptureGroup].Value;

            // parse target as long
            if (!long.TryParse(target, out long result))
                return null;

            return result;
        }

        /// <summary>
        /// data object for <see cref="GetBackupSizeAsync(JobInstance, bool, long)"/>
        /// </summary>
        public class JobSizeInfo
        {
            /// <summary>
            /// total data backed up in this job, in bytes
            /// (MSG_ID SNBJH_3311J)
            /// </summary>
            public long TotalDataBackedUp { get; internal set; } = 0;

            /// <summary>
            /// total data wwritten to tape in this job, in bytes
            /// (MSG_ID SNBJH_3313J)
            /// </summary>
            public long TotalDataOnMedia { get; internal set; } = 0;
        }
    }
}
