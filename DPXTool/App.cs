using CommandLine;
using DPXLib;
using DPXLib.Extension;
using DPXLib.Model.Common;
using DPXLib.Model.Constants;
using DPXLib.Model.JobInstances;
using DPXTool.Util;
using Refit;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main application class, parts:
    /// - Main
    /// - Shared
    /// </summary>
    public static partial class App
    {
        /// <summary>
        /// Base options that are shared between all verbs
        /// </summary>
        class BaseOptions
        {
            /// <summary>
            /// The DPX master server to connect to
            /// </summary>
            [Option('h', "host", Required = true, HelpText = "the DPX master server to connect to, FQDN with protocol. (ex. http://dpx-example.local)")]
            public string DPXHost { get; set; }

            /// <summary>
            /// The username to log in with
            /// </summary>
            [Option('u', "user", Required = true, HelpText = "the username to log in with")]
            public string Username { get; set; }

            /// <summary>
            /// The password to use for login. If not set, a interactive dialog is shown
            /// </summary>
            [Option('p', "password", Required = false, HelpText = "the password used for login. If not set, a interactive dialog is shown", Default = null)]
            public string Password { get; set; }

            /// <summary>
            /// file to write output to.
            /// either .csv or .html
            /// </summary>
            [Option('o', "output", Required = false, HelpText = "the file output is written to. can be either cvs or html format", Default = null)]
            public string OutputFile { get; set; }

            /// <summary>
            /// If true, a <see cref="HttpLoggingHandler"/> is used to intercept and log requests to debug out and file
            /// </summary>
            [Option("debug-requests", Required = false, HelpText = "debug communication with the dpx server and log them to a file", Default = false)]
            public bool DebugNetworkRequests { get; set; }

            /// <summary>
            /// if set, dont print tables to console
            /// </summary>
            [Option("no-console", Required = false, HelpText = "disable printing results to console", Default = false)]
            public bool NoPrintToConsole { get; set; }

            /// <summary>
            /// directory for offline log file mirrors
            /// </summary>
            [Option("logs-mirror-dir", Required = false, HelpText = "directory in which job log mirrors are saved in. Optional, tho makes loading of volsers and other stats faster.", Default = null)]
            public string OfflineLogsMirror { get; set; }
        }

        /// <summary>
        /// common options for job querying and filtering (used by <see cref="GetJobsOptions"/> and <see cref="JobStatsOptions"/>)
        /// </summary>
        class JobQueryOptions : BaseOptions
        {
            /// <summary>
            /// start date of the report
            /// </summary>
            [Option('s', "start", Required = true, HelpText = "start date for the report, format mm.dd.yyyy or mm.dd.yyyy,hh:mm:ss")]
            public DateTime ReportStart { get; set; }

            /// <summary>
            /// end date of the report
            /// </summary>
            [Option('e', "end", Required = false, HelpText = "end date for the report, format mm.dd.yyyy or mm.dd.yyyy,hh:mm:ss", Default = null)]
            public DateTime? ReportEnd { get; set; }

            /// <summary>
            /// job filter by job name
            /// </summary>
            [Option("job-name", Required = false, HelpText = "filter for jobs with these names. multiple possible")]
            public IEnumerable<string> FilterJobNames { get; set; }

            /// <summary>
            /// job filter by job run type (BASE, DIFF, INCR)
            /// </summary>
            [Option("job-type", Required = false, HelpText = "filter for jobs with this type (Base, Difr, Incr). multiple possible")]
            public IEnumerable<JobRunType> FilterJobRunTypes { get; set; }

            /// <summary>
            /// job filter by job status (Completed, Failed)
            /// </summary>
            [Option("job-status", Required = false, HelpText = "filter for jobs with this status (Failed, Completed, ...). multiple possible")]
            public IEnumerable<JobStatus> FilterJobStatus { get; set; }

            /// <summary>
            /// timeout for metadata query (eg logs)
            /// </summary>
            [Option("query-timeout", Required = false, HelpText = "timeout for getting metadata of a job instance; in milliseconds", Default = -1)]
            public long MetaQueryTimeout { get; set; }
        }

        /// <summary>
        /// Format string to convert DateTime into string using toString()
        /// </summary>
        const string DATETIME_FORMAT = @"yyyy.MM.dd, HH:mm:ss";

        /// <summary>
        /// Format strign to convert TimeSpan into string using toString();
        /// </summary>
        const string TIMESPAN_FORMAT = @"hh\:mm\:ss";

        /// <summary>
        /// the dpx client
        /// </summary>
        static DPXClient dpx;

        /// <summary>
        /// main entry point
        /// </summary>
        /// <param name="args">console arguments</param>
        public static void Main(string[] args)
        {
            //Demos.RunDemos().ConfigureAwait(false).GetAwaiter().GetResult();

            new Parser(o =>
            {
                o.HelpWriter = Parser.Default.Settings.HelpWriter;
                o.AutoHelp = true;
                o.AutoVersion = true;
                o.CaseInsensitiveEnumValues = true;
            }).ParseArguments<PrintLicenseOptions, GetJobsOptions, JobStatsOptions, GetLogsOptions, GetNodeGroupsOptions, GetNodesOptions>(args)
                .WithParsed<PrintLicenseOptions>(opt => PrintLicenseMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetJobsOptions>(opt => GetJobsMain(opt).GetAwaiter().GetResult())
                .WithParsed<JobStatsOptions>(opt => JobStatsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetLogsOptions>(opt => GetLogsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetNodeGroupsOptions>(opt => GetNodeGroupsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetNodesOptions>(opt => GetNodesMain(opt).GetAwaiter().GetResult());
        }

        /// <summary>
        /// initializes and logs in the <see cref="dpx"/>
        /// </summary>
        /// <param name="options">base options from the user</param>
        /// <returns>was init and login ok?</returns>
        static async Task<bool> InitClient(BaseOptions options)
        {
            //add http to hostname if not already
            if (!options.DPXHost.StartsWith("http") && !options.DPXHost.StartsWith("https"))
                options.DPXHost = "http://" + options.DPXHost;

            //init client
            dpx = new DPXClient(options.DPXHost, options.DebugNetworkRequests);
            dpx.DPXApiError += OnDPXApiError;

            //set offline log mirror if set and exists
            if (!string.IsNullOrWhiteSpace(options.OfflineLogsMirror) && Directory.Exists(options.OfflineLogsMirror))
                dpx.OfflineLogsMirrorsDirectory = options.OfflineLogsMirror;

            //get login password
            string pw = options.Password;
            if (string.IsNullOrWhiteSpace(pw))
                pw = ShowPasswordPrompt($"Password for {options.Username}@{options.DPXHost}: ");

            //log client in
            return await TryLoginAsync(options.Username, pw);
        }

        /// <summary>
        /// try to login the dpx client using username and password
        /// </summary>
        /// <param name="username">the username to login with</param>
        /// <param name="password">the password to login with</param>
        /// <returns>was login ok?</returns>
        static async Task<bool> TryLoginAsync(string username, string password)
        {
            //login client
            bool ok;
            try
            {
                ok = await dpx.LoginAsync(username, password);
            }
            catch (ApiException)
            {
                ok = false;
            }

            if (ok)
                Console.WriteLine("Login ok");
            else
                Console.WriteLine($"Failed to log client with username {username}! Is the password correct?");

            return ok;
        }

        /// <summary>
        /// query jobs using the filters defined in <see cref="JobQueryOptions"/> and then query their metadata.
        /// You have to initialize the dpx client first before calling this function!!
        /// 
        /// Job metadata in <see cref="JobWithMeta"/> may be null even when metaVolsers and/or metaSizeInfo are requested!
        /// </summary>
        /// <param name="options">the job filter options</param>
        /// <param name="onlyInRetention">should all jobs that are no longer in retention be removed from the list?</param>
        /// <param name="metaVolsers">should we query volser information?</param>
        /// <param name="metaSizeInfo">should we query size information?</param>
        /// <param name="metaTimeInfo">should we query phase time information?</param>
        /// <returns></returns>
        static async Task<List<JobWithMeta>> QueryJobsWithMeta(JobQueryOptions options,
            bool onlyInRetention = false,
            bool metaVolsers = false,
            bool metaSizeInfo = false,
            bool metaTimeInfo = false)
        {
            #region Build Filter
            //start and end times
            List<FilterItem> filters = new List<FilterItem>
            {
                FilterItem.ReportStart(options.ReportStart)
            };

            if (options.ReportEnd.HasValue)
                filters.Add(FilterItem.ReportEnd(options.ReportEnd.Value));

            //job filters
            string[] filterJobs = options.FilterJobNames.ToArray();
            if (filterJobs.Length > 0)
                filters.Add(FilterItem.JobNameIs(filterJobs));

            JobStatus[] filterStatus = options.FilterJobStatus.ToArray();
            if (filterStatus.Length > 0)
                filters.Add(FilterItem.JobStatus(filterStatus));
            #endregion

            #region get jobs from DPX api using filter
            Console.WriteLine("query jobs...");
            List<JobInstance> jobs = new List<JobInstance>();
            jobs.AddRange(await dpx.GetJobInstancesAsync(filters.ToArray()));

            //abort if no jobs were found
            if (jobs == null || jobs.Count <= 0)
            {
                Console.WriteLine("No jobs found!");
                return null;
            }
            #endregion

            #region Warn if dpx may not have records for the given start date
            //find the job with the earlyest run date, check how that compares to the report start time
            //dpx seems to only keep job information 30 days back
            JobInstance oldestJobInstance = jobs.First();
            foreach (JobInstance job in jobs)
                if (job.StartTime < oldestJobInstance.StartTime)
                    oldestJobInstance = job;

            Console.WriteLine($"Oldest job is {oldestJobInstance.Name} started {oldestJobInstance.StartTime.ToString(DATETIME_FORMAT)}");
            if (oldestJobInstance.StartTime.AddDays(-1) >= options.ReportStart)
            {
                ConsoleWriteColored("Oldest job found seems to be newer than selected start date! DPX may not have records reaching far enough.", ConsoleColor.Red);
            }
            #endregion

            #region Filter jobs further
            //filter jobs by status (no builtin way)
            List<JobRunType> filterRunTypes = options.FilterJobRunTypes.ToList();
            if (filterRunTypes.Count > 0)
                jobs = jobs.Where((job) => job.RunType.HasValue && filterRunTypes.Contains(job.RunType.Value)).ToList();

            //filter jobs that are already not in retention
            if (onlyInRetention)
                jobs = jobs.Where((job) => {
                    if ((DateTime.Now - job.EndTime).TotalDays >= job.Retention)
                    {
                        Console.WriteLine($"Job {job.ID} is out of retention!");
                        return false;
                    }
                    return true;
                }).ToList();

            //check there are still jobs
            if (jobs.Count <= 0)
            {
                Console.WriteLine("no jobs found!");
                return null;
            }
            #endregion

            #region query metadata for jobs
            // append jobs with metadata to results list (initialized at the top)
            List<JobWithMeta> jobsWithMeta = new List<JobWithMeta>();
            int i = 0;
            foreach (JobInstance job in jobs)
            {
                //create basic job (without metadata yet)
                JobWithMeta jobMeta = new JobWithMeta()
                {
                    Job = job
                };
                jobsWithMeta.Add(jobMeta);
                i++;
                Console.Write($"({i}/{jobs.Count}) processing job {job.ID}: ");

                // query volsers if needed
                if (metaVolsers)
                {
                    Console.Write("query volsers... ");
                    jobMeta.Volsers = await job.GetVolsersUsed(false, options.MetaQueryTimeout);
                }

                // query size info if needed
                if (metaSizeInfo)
                {
                    Console.Write("query size...");
                    jobMeta.Size = await job.GetBackupSizeAsync(false, options.MetaQueryTimeout);
                }

                // query phase times if needed
                if (metaTimeInfo)
                {
                    Console.Write("query times...");
                    jobMeta.TimeSpend = await job.GetJobTimingsAsync(options.MetaQueryTimeout);
                }

                Console.WriteLine();
            }
            #endregion

            // return null if no jobs were found (for some reason)
            if (jobsWithMeta.Count <= 0)
                return null;
            return jobsWithMeta;
        }

        /// <summary>
        /// a event invoked when a dpx api error occurs
        /// </summary>
        /// <param name="e">the exeption thrown by the api</param>
        /// <returns>should the call be retired? if false, the call is aborted and the exeption is thrown</returns>
        static bool OnDPXApiError(ApiException e)
        {
            //check if 401 unauthentificated
            if (e.StatusCode == HttpStatusCode.Unauthorized)
            {
                //re- login, token may have expired
                string pw = ShowPasswordPrompt($"re-enter password for {dpx.LoggedInUser}@{dpx.DPXHost}: ");
                return TryLoginAsync(dpx.LoggedInUser, pw).GetAwaiter().GetResult();
            }

            //dont handle any other errors
            return false;
        }

        /// <summary>
        /// write the table to a file defined by options
        /// </summary>
        /// <param name="options">the base options</param>
        /// <param name="writer">the table writer to write to file</param>
        static async Task WriteTableToFile(BaseOptions options, TableWriter writer)
        {
            //check writer is enabled
            if (string.IsNullOrWhiteSpace(options.OutputFile))
                return;

            //get path and check if is valid
            string path = options.OutputFile;
            if (!Path.IsPathFullyQualified(path))
                path = Path.GetFullPath(path);

            //get file extension
            string ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext))
            {
                Console.WriteLine("output file is is no file!");
                return;
            }

            //create file directory as needed
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            //write to file
            Console.WriteLine("Writing to " + path);
            ext = ext.ToLower().TrimStart('.');
            switch (ext)
            {
                case "html":
                case "htm":
                    await writer.WriteToFileAsync(path, TableWriter.TableFormat.HTML);
                    break;
                case "csv":
                    await writer.WriteToFileAsync(path, TableWriter.TableFormat.CSV);
                    break;
                default:
                    Console.WriteLine("unknown file type: " + ext + ". Supported types are html and csv");
                    break;
            }
        }

        /// <summary>
        /// show a password prompt
        /// </summary>
        /// <param name="message">the message to show on the prompt</param>
        /// <returns>the password entered</returns>
        static string ShowPasswordPrompt(string message)
        {
            //prompt user to enter password
            Console.Write(message);
            string pw = "";
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (pw.Length > 0)
                    {
                        pw = pw[0..^1];
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    pw += key.KeyChar;
                    Console.Write("*");
                }
            }

            Console.WriteLine();
            return pw;
        }

        /// <summary>
        /// Write a message to the console, but colored
        /// </summary>
        /// <param name="message">the message to write</param>
        /// <param name="color">the color to use</param>
        /// <param name="useColor">if false, the color is not changed</param>
        static void ConsoleWriteColored(string message, ConsoleColor color, bool useColor = true)
        {
            //save original color
            ConsoleColor original = Console.ForegroundColor;
            if (useColor)
                Console.ForegroundColor = color;

            //write message
            Console.WriteLine(message);

            //reset color
            if (useColor)
                Console.ForegroundColor = original;
        }

        /// <summary>
        /// a job with metadata attached
        /// </summary>
        class JobWithMeta
        {
            /// <summary>
            /// the job this contains metadata for
            /// </summary>
            public JobInstance Job { get; set; }

            /// <summary>
            /// volsers that this job was written to
            /// This may be null even when requested!
            /// </summary>
            public string[] Volsers { get; set; }

            /// <summary>
            /// information about the job size for this job
            /// This may be null even when requested!
            /// </summary>
            public JobSizeInfo Size { get; set; }

            /// <summary>
            /// information about the time the job spend in its different phases
            /// </summary>
            public JobTimeInfo TimeSpend { get; set; }
        }
    }
}
