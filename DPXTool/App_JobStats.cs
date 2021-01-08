using CommandLine;
using DPXTool.DPX;
using DPXTool.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode job-stats
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// options for the job-stats command
        /// </summary>
        [Verb("job-stats", HelpText = "get stats for backup jobs, like average size and fail / success rates")]
        class JobStatsOptions : JobQueryOptions
        {
            // no further options needed  
        }

        /// <summary>
        /// app mode: get job stats and averages
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task JobStatsMain(JobStatsOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //query the jobs with only their size as metadata
            List<JobWithMeta> jobsWithMeta = await QueryJobsWithMeta(options, false, false, true);

            // check we found at least one job
            if (jobsWithMeta == null || jobsWithMeta.Count <= 0)
            {
                Console.WriteLine("Did not find any jobs!");
                return;
            }

            // analyze (meta-) data of jobs
            Dictionary<string, JobStatsInfo> jobStats = new Dictionary<string, JobStatsInfo>();
            int jobsWithNoSizeInfo = 0;
            foreach (JobWithMeta jm in jobsWithMeta)
            {
                //get run name of job (jobname + runtype)
                string name = $"{jm.Job.Name}_{jm.Job.RunType}";

                //get stats object by name, create if not exists
                if (!jobStats.ContainsKey(name))
                    jobStats.Add(name, new JobStatsInfo());

                JobStatsInfo stats = jobStats[name];

                // add this job to stats:
                //size, if we have that info
                if (jm.Size != null)
                {
                    stats.AverageTotalData.Add(jm.Size.TotalDataBackedUp);
                    stats.AverageDataOnMedia.Add(jm.Size.TotalDataOnMedia);
                }
                else
                {
                    Console.WriteLine($"Job {name} ({jm.Job.ID}) does not have any size information!");
                    jobsWithNoSizeInfo++;
                }

                //run time stats
                stats.AverageRunTime.Add(jm.Job.RunDuration / 1000.0);
                stats.AddRunDate(jm.Job.StartTime);

                //status counting
                if (jm.Job.GetStatus().IsFailedStatus())
                    stats.FailedRuns++;
                else
                    stats.SuccessfulRuns++;
            }

            //add info when more than 10% of jobs did not have size metadata (this can be because of too low --query-timeout or slow network)
            if (jobsWithNoSizeInfo >= Math.Floor(jobsWithMeta.Count * 0.1))
                ConsoleWriteColored($"\n{jobsWithNoSizeInfo} Jobs (> 10%) do not have size information associated with them!\n" +
                    "Try increasing the --query-timeout, as a too low timeout OR slow network can cause those problems.\n",
                    ConsoleColor.Red);

            //init table
            TableWriter w = new TableWriter();

            //write nodes
            w.WriteRow("Job",
                "Total Data (Average)",
                "Data on Tape (Average)",
                "Run Time (Average)",
                "Time Between Runs (Average)",
                "Successfull Runs",
                "Failed Runs",
                "Success Rate");
            foreach (string name in jobStats.Keys)
                if (jobStats.TryGetValue(name, out JobStatsInfo stats))
                {
                    w.WriteRow(name,
                        stats.AverageTotalData.Average.ToFileSize(),
                        stats.AverageDataOnMedia.Average.ToFileSize(),
                        TimeSpan.FromSeconds(stats.AverageRunTime.Average).ToString(TIMESPAN_FORMAT),
                        stats.AverageTimeBetweenRuns.ToString(TIMESPAN_FORMAT),
                        stats.SuccessfulRuns + "",
                        stats.FailedRuns + "",
                        stats.SuccessRate + " %");
                }


            //write table
            if (!options.NoPrintToConsole)
                w.WriteToConsole();
            await WriteTableToFile(options, w);
        }

        /// <summary>
        /// stats for a job
        /// </summary>
        class JobStatsInfo
        {
            /// <summary>
            /// average data backed up in this job, in bytes
            /// </summary>
            public AverageNumber AverageTotalData { get; } = new AverageNumber();

            /// <summary>
            /// average data wwritten to tape in this job, in bytes
            /// </summary>
            public AverageNumber AverageDataOnMedia { get; } = new AverageNumber();

            /// <summary>
            /// how long the job runs on average, in seconds
            /// </summary>
            public AverageNumber AverageRunTime { get; } = new AverageNumber();

            /// <summary>
            /// how often the job ran successful
            /// </summary>
            public long SuccessfulRuns { get; set; } = 0;

            /// <summary>
            /// how often the job failed
            /// </summary>
            public long FailedRuns { get; set; } = 0;

            /// <summary>
            /// how many percent the job runs successfull
            /// </summary>
            public double SuccessRate
            {
                get
                {
                    return 100.0 * SuccessfulRuns / (SuccessfulRuns + FailedRuns);
                }
            }

            #region Logic for time between 
            /// <summary>
            /// internal list for dates the job ran, for time between calculation
            /// </summary>
            private List<DateTime> runTimes = new List<DateTime>();

            /// <summary>
            /// the average time between runs of this job
            /// </summary>
            public TimeSpan AverageTimeBetweenRuns
            {
                get
                {
                    // check we have at least two times we ran
                    if (runTimes == null || runTimes.Count < 2)
                        return TimeSpan.Zero;

                    //sort times
                    runTimes.Sort((a, b) => DateTime.Compare(a, b));

                    // enumerate all times, excluding last one
                    AverageNumber avg = new AverageNumber();
                    for (int i = 0; i < runTimes.Count - 1; i++)
                    {
                        // get time of current and the following job
                        DateTime now = runTimes[i];
                        DateTime next = runTimes[i + 1];

                        // calculate difference between run dates, add to average
                        avg.Add((next - now).TotalSeconds);
                    }

                    // return average
                    return TimeSpan.FromSeconds(avg.Average);
                }
            }

            /// <summary>
            /// add a time this job ran, for time between runs calculation
            /// </summary>
            /// <param name="date">the time to add</param>
            public void AddRunDate(DateTime date)
            {
                runTimes.Add(date);
            }
            #endregion
        }
    }
}
