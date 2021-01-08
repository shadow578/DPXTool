using CommandLine;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.Util;
using System;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode get-logs
    /// </summary>
    public partial class App
    {
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
                w.WriteRow(log.SourceIP, log.Time.ToString(DATETIME_FORMAT), log.Module, log.MessageCode, log.Message);

            //write table
            Console.WriteLine("logs for job " + options.JobInstanceID);
            if (!options.NoPrintToConsole)
                w.WriteToConsole();
            await WriteTableToFile(options, w);
        }
    }
}
