using CommandLine;
using DPXTool.DPX;
using DPXTool.DPX.Model.Common;
using DPXTool.DPX.Model.Constants;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.DPX.Model.License;
using DPXTool.DPX.Model.Nodes;
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
    /// Main application class
    /// </summary>
    public static class App
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
        }

        /// <summary>
        /// Options for the get-license command
        /// </summary>
        [Verb("get-license", HelpText = "get license information")]
        class PrintLicenseOptions : BaseOptions
        {
            //no additional options needed
        }

        /// <summary>
        /// Options for the get-jobs command
        /// </summary>
        [Verb("get-jobs", HelpText = "get jobs and information about them")]
        class GetJobsOptions : BaseOptions
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
            /// job filter by job type (BLOCK, NDMP)
            /// </summary>
            [Option("job-protocol", Required = false, HelpText = "filter for jobs with these protocols (Block, File, NDMP, Catalog, ...). multiple possible")]
            public IEnumerable<JobType> FilterJobTypes { get; set; }

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

            /// <summary>
            /// timeout for metadata query (eg logs; used by volsers and backup size)
            /// </summary>
            [Option("meta-query-timeout", Required = false, HelpText = "timeout for getting metadata of a job instance; used for volsers and backup size; in milliseconds", Default = -1)]
            public long MetaQueryTimeout { get; set; }
        }

        /// <summary>
        /// Options for the get-logs command
        /// </summary>
        [Verb("get-logs", HelpText = "get logs for a job by job id")]
        class GetLogsOptions : BaseOptions
        {
            /// <summary>
            /// job instance id to get logs of
            /// </summary>
            [Option('j', "job-id", Required = true, HelpText = "the job instance id to get logs of")]
            public long JobInstanceID { get; set; }

            /// <summary>
            /// index of the first log to get
            /// </summary>
            [Option('s', "start", Required = false, HelpText = "the index of the first log entry to get", Default = 0)]
            public long LogStartIndex { get; set; }

            /// <summary>
            /// how many log entries to get
            /// </summary>
            [Option('c', "count", Required = false, HelpText = "how many log entries to get", Default = 500)]
            public long LogCount { get; set; }

            /// <summary>
            /// should we get all log entries?
            /// </summary>
            [Option('a', "all-logs", Required = false, HelpText = "get all log entries? if set, --start and --count are overwritten", Default = false)]
            public bool GetAllLogs { get; set; }
        }

        /// <summary>
        /// Options for the get-node-groups command
        /// </summary>
        [Verb("get-node-groups", HelpText = "get a list of all node groups")]
        class GetNodeGroupsOptions : BaseOptions
        {
            //no additional options needed
        }

        /// <summary>
        /// Options for the get-nodes command
        /// </summary>
        [Verb("get-nodes", HelpText = "get information about nodes")]
        class GetNodesOptions : BaseOptions
        {
            /// <summary>
            /// name of the node to print.
            /// If set, NodeGroup and NodeType are ignored
            /// </summary>
            [Option('n', "name", Required = false, HelpText = "name of the node to print. If set, node-group and node-type are ignored", Default = null)]
            public string NodeName { get; set; }

            /// <summary>
            /// the node group to print nodes of
            /// </summary>
            [Option('g', "node-group", Required = false, HelpText = "the node group to print all nodes of. works in conjunction with node-group", Default = null)]
            public string NodeGroup { get; set; }

            /// <summary>
            /// the node type to print all nodes of
            /// </summary>
            [Option('t', "node-type", Required = false, HelpText = "the type of node to print nodes of. works in conjunction with node-group", Default = null)]
            public string NodeType { get; set; }
        }

        /// <summary>
        /// Format string to convert DateTime into string using toString()
        /// </summary>
        const string DATE_FORMAT = "yyyy.MM.dd, HH:mm:ss";

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
            new Parser(o =>
            {
                o.HelpWriter = Parser.Default.Settings.HelpWriter;
                o.AutoHelp = true;
                o.AutoVersion = true;
                o.CaseInsensitiveEnumValues = true;
            }).ParseArguments<PrintLicenseOptions, GetJobsOptions, GetLogsOptions, GetNodeGroupsOptions, GetNodesOptions>(args)
                .WithParsed<PrintLicenseOptions>(opt => PrintLicenseMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetJobsOptions>(opt => GetJobsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetLogsOptions>(opt => GetLogsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetNodeGroupsOptions>(opt => GetNodeGroupsMain(opt).GetAwaiter().GetResult())
                .WithParsed<GetNodesOptions>(opt => GetNodesMain(opt).GetAwaiter().GetResult());
        }

        /// <summary>
        /// app mode: print license information
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task PrintLicenseMain(PrintLicenseOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //get license details
            Console.WriteLine("query license...");
            LicenseResponse l = await dpx.GetLicenseInfoAsync();

            #region write license details to console
            Console.WriteLine($@"
License Details for {l.ServerHostName}:
 Node: {l.ServerNodeName} ({l.ServerNodeAddress})
 Version: {l.DPXVersion} (built {l.DPXBuildDate} at {l.DPXBuildTime})
 Is Evaluation License: {(l.IsEvalLicanse ? "Yes" : "No")}
 License Expires in: {l.ExpiresInDays} days 
 
Licensed Categories:");
            foreach (LicenseCategory c in l.LicenseCategories)
                ConsoleWriteColored($" {c.Name}: {c.Consumed} of {c.Licensed}", ConsoleColor.Red, c.IsLicenseViolated);
            #endregion

            #region write license details to file
            //write license categories
            TableWriter w = new TableWriter();
            w.WriteRow("Node", "Category", "Consumed", "Licensed", "License Violation");
            foreach (LicenseCategory c in l.LicenseCategories)
                w.WriteRow(l.ServerNodeName, c.Name, c.Consumed.ToString(), c.Licensed.ToString(), c.IsLicenseViolated ? "YES" : "NO");

            //write file
            await WriteTableToFile(options, w);
            #endregion
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

            #region Build Filter
            //start and end times
            List<FilterItem> filters = new List<FilterItem>();
            filters.Add(FilterItem.ReportStart(options.ReportStart));

            if (options.ReportEnd.HasValue)
                filters.Add(FilterItem.ReportEnd(options.ReportEnd.Value));

            //job filters
            string[] filterJobs = options.FilterJobNames.ToArray();
            if (filterJobs.Length > 0)
                filters.Add(FilterItem.JobNameIs(filterJobs));

            JobType[] filterTypes = options.FilterJobTypes.ToArray();
            if (filterTypes.Length > 0)
                filters.Add(FilterItem.JobType(filterTypes));

            JobStatus[] filterStatus = options.FilterJobStatus.ToArray();
            if (filterStatus.Length > 0)
                filters.Add(FilterItem.JobStatus(filterStatus));
            #endregion

            //get jobs
            Console.WriteLine("query jobs...");
            List<JobInstance> jobs = new List<JobInstance>();
            jobs.AddRange(await dpx.GetJobInstancesAsync(filters.ToArray()));

            //abort if no jobs were found
            if (jobs == null || jobs.Count <= 0)
            {
                Console.WriteLine("No jobs found!");
                return;
            }

            #region Warn if dpx may not have records for the given start date
            //find the job with the earlyest run date, check how that compares to the report start time
            //dpx seems to only keep job information 30 days back
            JobInstance oldestJobInstance = jobs.First();
            foreach (JobInstance job in jobs)
                if (job.StartTime < oldestJobInstance.StartTime)
                    oldestJobInstance = job;

            Console.WriteLine($"Oldest job is {oldestJobInstance.Name} started {oldestJobInstance.StartTime.ToString(DATE_FORMAT)}");
            if (oldestJobInstance.StartTime.AddDays(-1) >= options.ReportStart)
            {
                ConsoleWriteColored("Oldest job found seems to be newer than selected start date! DPX may not have records reaching far enough.", ConsoleColor.Red);
            }
            #endregion

            //filter jobs by status (no builtin way)
            List<JobRunType> filterRunTypes = options.FilterJobRunTypes.ToList();
            if (filterRunTypes.Count > 0)
                jobs = jobs.Where((job) => job.RunType.HasValue && filterRunTypes.Contains(job.RunType.Value)).ToList();

            //filter jobs that are already not in retention
            if (options.ShouldOnlyListInRetention)
                foreach (JobInstance job in jobs)
                    if ((DateTime.Now - job.EndTime).TotalDays >= job.Retention)
                    {
                        Console.WriteLine($"Job {job.ID} is out of retention!");
                        jobs.Remove(job);
                    }

            //check there are still jobs
            if (jobs.Count <= 0)
            {
                Console.WriteLine("no jobs found!");
                return;
            }

            //query metadata for jobs
            //JobWithMeta is also used if no metadata is attached, to simplify the code *a little*
            List<JobWithMeta> jobsAndMeta = new List<JobWithMeta>();
            int i = 0;
            foreach (JobInstance job in jobs)
            {
                //create basic job (without metadata yet)
                JobWithMeta jobMeta = new JobWithMeta()
                {
                    Job = job
                };
                jobsAndMeta.Add(jobMeta);

                // query volsers if needed
                if (options.ShouldIncludeVolsers || options.ShouldOnlyListVolsers)
                {
                    Console.WriteLine($"({++i}/{jobs.Count}) query volsers for job {job.ID}");
                    jobMeta.Volsers = await job.GetVolsersUsed(false, options.MetaQueryTimeout);
                }

                // query size info if needed
                if(options.ShouldIncludeSizeInfo && !options.ShouldOnlyListVolsers)
                {
                    Console.WriteLine($"({++i}/{jobs.Count}) query size info for job {job.ID}");
                    jobMeta.Size = await job.GetBackupSizeAsync(false, options.MetaQueryTimeout);
                }
            }

            //split print in full and only volsers mode
            if (options.ShouldOnlyListVolsers)
            {
                //get volsers first, unify into one list
                Dictionary<string /*volser*/, int /*use count*/> allVolsers = new Dictionary<string, int>();
                foreach (JobWithMeta job in jobsAndMeta)
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
                    "Total Files Backed up", 
                    "", 
                    "", 
                    "Volsers Used");
                foreach (JobWithMeta m in jobsAndMeta)
                {
                    //build volser string
                    string volsersStr = "-";
                    if (m.Volsers != null && m.Volsers.Length > 0)
                        volsersStr = string.Join(", ", m.Volsers);

                    //write to table
                    w.WriteRow(m.Job.StartTime.ToString(DATE_FORMAT), 
                        m.Job.EndTime.ToString(DATE_FORMAT), 
                        TimeSpan.FromMilliseconds(m.Job.RunDuration).ToString(),
                        m.Job.ID.ToString(), 
                        m.Job.DisplayName,
                        m.Job.JobType.ToString(), 
                        m.Job.RunType.ToString(), 
                        m.Job.Retention.ToString(),
                        (DateTime.Now - m.Job.EndTime).TotalDays.ToString("0"),
                        m.Job.ReturnCode.ToString(), 
                        m.Job.GetStatus().ToString(),

                        m.Size == null ? "-" : m.Size.FilesBackedUp.ToString(),
                        m.Size == null ? "-" : m.Size.TotalDataBackedUp.ToFileSize(),
                        m.Size == null ? "-" : m.Size.TotalDataOnMedia.ToFileSize(),

                        volsersStr);
                }

                //write table
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
            foreach (JobInstance job in jobs)
            {
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
                    Console.WriteLine($@" BASE: {lastBase.ID} finished {(DateTime.Now - lastBase.EndTime).TotalDays:0} days ago on {lastBase.EndTime.ToString(DATE_FORMAT)}");
                else
                    ConsoleWriteColored(" no BASE backup found!", ConsoleColor.Red);
                if (lastDifr != null)
                    Console.WriteLine($@" DIFR: {lastDifr.ID} finished {(DateTime.Now - lastDifr.EndTime).TotalDays:0} days ago on {lastDifr.EndTime.ToString(DATE_FORMAT)}");
                else
                    ConsoleWriteColored(" no DIFR backup found!", ConsoleColor.Red);
                if (lastIncr != null)
                    Console.WriteLine($@" INCR: {lastIncr.ID} finished {(DateTime.Now - lastIncr.EndTime).TotalDays:0} days ago on {lastIncr.EndTime.ToString(DATE_FORMAT)}");
                else
                    ConsoleWriteColored(" no INCR backup found!", ConsoleColor.Red);
            }
            else
                Console.WriteLine("instances of more than one job were found, last run statistics not available.");
            #endregion
        }

        /// <summary>
        /// app mode: get logs for a job
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task GetLogsMain(GetLogsOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //check job id seems valid
            if (options.JobInstanceID <= 0)
            {
                Console.WriteLine($"job id {options.JobInstanceID} is invalid!");
                return;
            }

            //get logs for job
            InstanceLogEntry[] logs;
            if (options.GetAllLogs)
                logs = await dpx.GetAllJobInstanceLogsAsync(options.JobInstanceID);
            else
                logs = await dpx.GetJobInstanceLogsAsync(options.JobInstanceID, options.LogStartIndex, options.LogCount);

            //init table
            TableWriter w = new TableWriter();

            //write license categories
            w.WriteRow("Source IP", "Time", "Module", "Message Code", "Message");
            foreach (InstanceLogEntry log in logs)
                w.WriteRow(log.SourceIP, log.Time.ToString(DATE_FORMAT), log.Module, log.MessageCode, log.Message);

            //write table
            Console.WriteLine("logs for job " + options.JobInstanceID);
            w.WriteToConsole();
            await WriteTableToFile(options, w);
        }

        /// <summary>
        /// app mode: get node groups
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task GetNodeGroupsMain(GetNodeGroupsOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //get node groups
            Console.WriteLine("query node groups...");
            NodeGroup[] groups = await dpx.GetNodeGroupsAsync();

            //init table
            TableWriter w = new TableWriter();

            //write node groups
            w.WriteRow("Group Name", "Comment", "Media Pool", "Cluster", "Creator", "Creation Time");
            foreach (NodeGroup g in groups)
                w.WriteRow(g.Name, g.Comment, g.MediaPool, g.ClusterName, g.Creator, g.CreationTime.ToString(DATE_FORMAT));

            //write table
            Console.WriteLine($"Found {groups.Length} node groups:");
            w.WriteToConsole();
            await WriteTableToFile(options, w);
        }

        /// <summary>
        /// app mode: get nodes
        /// </summary>
        /// <param name="options">options from the command line</param>
        static async Task GetNodesMain(GetNodesOptions options)
        {
            //initialize dpx client
            if (!await InitClient(options))
                return;

            //get nodes
            Console.WriteLine("query nodes...");
            Node[] nodes;
            if (!string.IsNullOrWhiteSpace(options.NodeName))
                nodes = (await dpx.GetNodesAsync()).Where((n) => n.Name.Equals(options.NodeName, StringComparison.OrdinalIgnoreCase)).ToArray();
            else
                nodes = await dpx.GetNodesAsync(options.NodeGroup, options.NodeType);

            //init table
            TableWriter w = new TableWriter();

            //write nodes
            w.WriteRow("Node Group", "Node", "Server Name", "Node Type", "OS", "OS Name", "OS Version", "Creator", "Creation Time", "Comments");
            foreach (Node n in nodes)
                w.WriteRow(n.GroupName, n.Name, n.ServerName, n.Type, n.OSGroup.ToString(), n.OSDisplayName, n.OSVersion, n.Creator, n.CreationTime.ToString(DATE_FORMAT), n.Comment);


            //write table
            Console.WriteLine($"Found {nodes.Length} nodes:");
            w.WriteToConsole();
            await WriteTableToFile(options, w);
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
                        pw = pw.Substring(0, pw.Length - 1);
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
            /// </summary>
            public string[] Volsers { get; set; }

            /// <summary>
            /// information about the job size for this job
            /// </summary>
            public DPXExtensions.JobSizeInfo Size { get; set; }
        }
    }
}
