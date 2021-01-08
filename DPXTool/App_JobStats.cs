using CommandLine;
using DPXTool.DPX;
using DPXTool.DPX.Extension;
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
        [Verb("job-stats", HelpText = "get stats for backup jobs, like average size, time spend on phases, and fail / success rates")]
        class JobStatsOptions : JobQueryOptions
        {
            /// <summary>
            /// disable formatting data sizes in table.
            /// if true, always use KB as data unit
            /// </summary>
            [Option("no-data-units", Required = false, HelpText = "disable formatting data sizes with data units. If set, KB will be used exclusively.", Default = false)]
            public bool DontFormatDataUnits { get; set; }
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
            List<JobWithMeta> jobsWithMeta = await QueryJobsWithMeta(options, metaSizeInfo: true, metaTimeInfo: true);

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
                // size, if we have that info
                if (jm.Size != null)
                {
                    stats.TotalData.Add(jm.Size.TotalDataBackedUp);
                    stats.DataOnMedia.Add(jm.Size.TotalDataOnMedia);
                }
                else
                {
                    Console.WriteLine($"Job {name} ({jm.Job.ID}) does not have any size information!");
                    jobsWithNoSizeInfo++;
                }

                //run time stats
                stats.AddRunDate(jm.Job.StartTime);
                stats.AddJobTimeInfo(jm.TimeSpend);

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
                "Time Between Runs (Average)",
                "Job Duration (Average)",
                "Time Spend Initializing (Average)",
                "Time Spend Waiting (Average)",
                "Time Spend Preprocessing (Average)",
                "Time Spend Transferring Data (Average)",
                "Successfull Runs",
                "Failed Runs",
                "Success Rate");
            foreach (string name in jobStats.Keys)
                if (jobStats.TryGetValue(name, out JobStatsInfo stats))
                {
                    //get data size strings, default to static KB unit
                    string totalData = Math.Floor(stats.TotalData.Average / 1000) + "KB";
                    string dataOnMedia = Math.Floor(stats.DataOnMedia.Average / 1000) + "KB";
                    if (!options.DontFormatDataUnits)
                    {
                        // use dynamic units
                        totalData = stats.TotalData.Average.ToDataSize();
                        dataOnMedia = stats.DataOnMedia.Average.ToDataSize();
                    }

                    //write table row
                    w.WriteRow(name,
                        totalData,
                        dataOnMedia,
                        stats.AverageTimeBetweenRuns.ToString(TIMESPAN_FORMAT),
                        stats.AveragePhaseTimes.Total.ToString(TIMESPAN_FORMAT),
                        stats.AveragePhaseTimes.Initializing.ToString(TIMESPAN_FORMAT),
                        stats.AveragePhaseTimes.Waiting.ToString(TIMESPAN_FORMAT),
                        stats.AveragePhaseTimes.Preprocessing.ToString(TIMESPAN_FORMAT),
                        stats.AveragePhaseTimes.Transferring.ToString(TIMESPAN_FORMAT),
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
            public AverageNumber TotalData { get; } = new AverageNumber();

            /// <summary>
            /// average data wwritten to tape in this job, in bytes
            /// </summary>
            public AverageNumber DataOnMedia { get; } = new AverageNumber();

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

            #region Phase ime Statistics
            /// <summary>
            /// internal list of job timings, for time statistics
            /// </summary>
            private List<JobTimeInfo> jobTimes = new List<JobTimeInfo>();

            /// <summary>
            /// get the average time this job spend on its different phases
            /// </summary>
            public JobTimeInfo AveragePhaseTimes
            {
                get
                {
                    // check we have at least one time info 
                    if (jobTimes == null || jobTimes.Count <= 0)
                        return new JobTimeInfo();

                    // prepare variables for different phases
                    // save total seconds
                    double total = 0,
                        init = 0,
                        wait = 0,
                        preprocess = 0,
                        transfer = 0;

                    // add all phase times to totals
                    foreach (JobTimeInfo time in jobTimes)
                    {
                        total += time.Total.TotalSeconds;
                        init += time.Initializing.TotalSeconds;
                        wait += time.Waiting.TotalSeconds;
                        preprocess += time.Preprocessing.TotalSeconds;
                        transfer += time.Transferring.TotalSeconds;
                    }

                    // divide all totals by the number of jobs that ran (that we know of)
                    double count = jobTimes.Count;
                    total /= count;
                    init /= count;
                    wait /= count;
                    preprocess /= count;
                    transfer /= count;

                    // return a JobTimeInfo object with the averages
                    return new JobTimeInfo()
                    {
                        Total = TimeSpan.FromSeconds(total),
                        Initializing = TimeSpan.FromSeconds(init),
                        Waiting = TimeSpan.FromSeconds(wait),
                        Preprocessing = TimeSpan.FromSeconds(preprocess),
                        Transferring = TimeSpan.FromSeconds(transfer)
                    };
                }
            }

            /// <summary>
            /// add a job time info to this jobs statistics
            /// </summary>
            /// <param name="time">the job time to add</param>
            public void AddJobTimeInfo(JobTimeInfo time)
            {
                jobTimes.Add(time);
            }
            #endregion

            #region Time between runs
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
