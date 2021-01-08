using CommandLine;
using DPXTool.DPX.Model.Constants;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode get-jobs
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Options for the get-jobs command
        /// </summary>
        [Verb("get-jobs", HelpText = "get jobs and information about them")]
        class GetJobsOptions : JobQueryOptions
        {
            /// <summary>
            /// should used volsers be included in the report?
            /// </summary>
            [Option("include-volsers", Required = false, HelpText = "include volsers used in the report?", Default = false)]
            public bool ShouldIncludeVolsers { get; set; }

            /// <summary>
            /// should the report only be a list of volsers used?
            /// </summary>
            [Option("only-volsers", Required = false, HelpText = "only list used volsers in the report?", Default = false)]
            public bool ShouldOnlyListVolsers { get; set; }

            /// <summary>
            /// should size info for the job be included in the report?
            /// </summary>
            [Option("include-size", Required = false, HelpText = "include job size info in the report?", Default = false)]
            public bool ShouldIncludeSizeInfo { get; set; }

            /// <summary>
            /// should only jobs that are still in retention be listed?
            /// </summary>
            [Option("only-in-retention", Required = false, HelpText = "only include jobs that are still in retention", Default = false)]
            public bool ShouldOnlyListInRetention { get; set; }
        }

        /// <summary>
        /// app mode: get information about jobs
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task GetJobsMain(GetJobsOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //query the jobs with their metadata
            List<JobWithMeta> jobsWithMeta = await QueryJobsWithMeta(options, options.ShouldOnlyListInRetention,
                options.ShouldIncludeVolsers | options.ShouldOnlyListVolsers,
                options.ShouldIncludeSizeInfo & !options.ShouldOnlyListVolsers);// no need to query size info if we won't use it anyways

            // check we found at leas one job
            if (jobsWithMeta == null || jobsWithMeta.Count <= 0)
            {
                Console.WriteLine("Did not find any jobs!");
                return;
            }

            //split print in full and only volsers mode
            if (options.ShouldOnlyListVolsers)
            {
                //get volsers first, unify into one list
                Dictionary<string /*volser*/, int /*use count*/> allVolsers = new Dictionary<string, int>();
                foreach (JobWithMeta job in jobsWithMeta)
                {
                    string[] volsers = job.Volsers;
                    if (volsers != null && volsers.Length > 0)
                        foreach (string volser in volsers)
                            if (!allVolsers.ContainsKey(volser))
                                allVolsers.Add(volser, 1);
                            else
                                allVolsers[volser]++;
                }

                //init table
                TableWriter w = new TableWriter();

                //write volsers categories
                w.WriteRow("Volser", "Used by Jobs");
                foreach (string volser in allVolsers.Keys)
                    w.WriteRow(volser, allVolsers[volser].ToString());

                //write table
                if (!options.NoPrintToConsole)
                    w.WriteToConsole();
                await WriteTableToFile(options, w);
            }
            else
            {
                //init table
                TableWriter w = new TableWriter();

                //write license categories
                w.WriteRow("Start Time",
                    "End Time",
                    "Duration",
                    "ID",
                    "Name",
                    "Protocol",
                    "Type",
                    "Retention (days)",
                    "Days since run",
                    "RC",
                    "Status",
                    "Data Backed up",
                    "Data on Tape",
                    "Volsers Used");
                foreach (JobWithMeta m in jobsWithMeta)
                {
                    //build volser string
                    string volsersStr = "-";
                    if (m.Volsers != null && m.Volsers.Length > 0)
                        volsersStr = string.Join(", ", m.Volsers);

                    //write to table
                    w.WriteRow(m.Job.StartTime.ToString(DATETIME_FORMAT),
                        m.Job.EndTime.ToString(DATETIME_FORMAT),
                        TimeSpan.FromMilliseconds(m.Job.RunDuration).ToString(TIMESPAN_FORMAT),
                        m.Job.ID.ToString(),
                        m.Job.DisplayName,
                        m.Job.JobType.ToString(),
                        m.Job.RunType.ToString(),
                        m.Job.Retention.ToString(),
                        (DateTime.Now - m.Job.EndTime).TotalDays.ToString("0"),
                        m.Job.ReturnCode.ToString(),
                        m.Job.GetStatus().ToString(),

                        m.Size == null ? "-" : m.Size.TotalDataBackedUp.ToFileSize(),
                        m.Size == null ? "-" : m.Size.TotalDataOnMedia.ToFileSize(),

                        volsersStr);
                }

                //write table
                if (!options.NoPrintToConsole)
                    w.WriteToConsole();
                await WriteTableToFile(options, w);
            }

            #region print last backup run times
            //check only instances of one job were found (by filter or coincidence i guess :P)
            //get latest backup jobs by type
            bool onlyOneJob = true;
            string jobName = string.Empty;
            JobInstance lastBase = null,
                lastDifr = null,
                lastIncr = null;
            foreach (JobWithMeta jobWithMeta in jobsWithMeta)
            {
                //get job instance
                JobInstance job = jobWithMeta.Job;

                //check only instances of one job
                if (string.IsNullOrWhiteSpace(jobName))
                    jobName = job.Name;
                else if (!jobName.Equals(job.Name))
                {
                    onlyOneJob = false;
                    break;
                }

                //get last jobs
                switch (job.RunType)
                {
                    case JobRunType.BASE:
                        if (lastBase == null || job.EndTime > lastBase.EndTime)
                            lastBase = job;
                        break;
                    case JobRunType.DIFR:
                        if (lastDifr == null || job.EndTime > lastDifr.EndTime)
                            lastDifr = job;
                        break;
                    case JobRunType.INCR:
                        if (lastIncr == null || job.EndTime > lastIncr.EndTime)
                            lastIncr = job;
                        break;
                }
            }

            //print to ui if only one job
            if (onlyOneJob)
            {
                Console.WriteLine(@$"last backup runs{(options.ShouldOnlyListInRetention ? " still in retention" : "")}:");
                if (lastBase != null)
                    Console.WriteLine($@" BASE: {lastBase.ID} finished {(DateTime.Now - lastBase.EndTime).TotalDays:0} days ago on {lastBase.EndTime.ToString(DATETIME_FORMAT)}");
                else
                    ConsoleWriteColored(" no BASE backup found!", ConsoleColor.Red);
                if (lastDifr != null)
                    Console.WriteLine($@" DIFR: {lastDifr.ID} finished {(DateTime.Now - lastDifr.EndTime).TotalDays:0} days ago on {lastDifr.EndTime.ToString(DATETIME_FORMAT)}");
                else
                    ConsoleWriteColored(" no DIFR backup found!", ConsoleColor.Red);
                if (lastIncr != null)
                    Console.WriteLine($@" INCR: {lastIncr.ID} finished {(DateTime.Now - lastIncr.EndTime).TotalDays:0} days ago on {lastIncr.EndTime.ToString(DATETIME_FORMAT)}");
                else
                    ConsoleWriteColored(" no INCR backup found!", ConsoleColor.Red);
            }
            else
                Console.WriteLine("instances of more than one job were found, last run statistics not available.");
            #endregion
        }
    }
}
