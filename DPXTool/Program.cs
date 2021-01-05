using DPXTool.DPX;
using DPXTool.DPX.Model.Common;
using DPXTool.DPX.Model.Constants;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.DPX.Model.License;
using DPXTool.DPX.Model.Nodes;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// contains early usage demos and testts for the DPXClient class.
    /// Just staying here as a reference, may be removed later :P
    /// </summary>
    [Obsolete("early usage demo and tests for DPX Client")]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Demo Functions are for demo :P")]
    class Program
    {
        static DPXClient client;

        public static async Task RunDemos()
        {
            //get info from user
            Console.Write("enter host name (http://dpx-example.local): ");
            string host = Console.ReadLine();
            Console.Write("enter username: ");
            string user = Console.ReadLine();
            Console.Write($"{user}@{host}'s password: ");
            string pw = Console.ReadLine();

            //init and login
            client = new DPXClient(host, true);
            bool loginOk = await client.LoginAsync(user, pw);
            Console.WriteLine("login ok: " + loginOk);

            //demos
            //await DemoLicense();
            //await DemoJobs();
            //await DemoLogs();
            //await DemoVolsers();
            //await DemoNodes();
            //await DemoBackupSize();
            await DemoLogsCached();

            Console.WriteLine("end");
            Console.ReadLine();
        }


        /// <summary>
        /// demo for GetLicenseInfoAsync()
        /// </summary>
        static async Task DemoLicense()
        {
            //get license
            LicenseResponse lic = await client.GetLicenseInfoAsync();

            //check if valid response
            if (lic == null)
            {
                Console.WriteLine("Server returned no license info");
                return;
            }

            //print license info
            Console.WriteLine(@$"
Info for {lic.ServerHostName}:
DPX Version: {lic.DPXVersion} ({lic.DPXBuildDate} {lic.DPXBuildTime})
Node:        {lic.ServerNodeName} ({lic.ServerNodeAddress})
Eval:        {(lic.IsEvalLicanse ? "Yes" : "No")}
Expires in:  {lic.ExpiresInDays} days

Licensed:");

            foreach (LicenseCategory cat in lic.LicenseCategories)
            {
                ConsoleWriteLineC(@$"   {cat.Name}:  {cat.Consumed} of {cat.Licensed}", ConsoleColor.Red, cat.IsLicenseViolated);
            }
        }

        /// <summary>
        /// demo for GetJobInstances()
        /// </summary>
        static async Task DemoJobs()
        {
            //get jobs
            JobInstance[] jobs = await client.GetJobInstancesAsync(FilterItem.ReportStart(new DateTime(2020, 9, 1)));

            Console.WriteLine($"Found total of {jobs.Length} jobs:");
            foreach (JobInstance job in jobs)
            {
                Console.WriteLine(@$"   {job.DisplayName}");
            }
        }

        /// <summary>
        /// demo for GetJobInstanceLogs()
        /// </summary>
        static async Task DemoLogs()
        {
            //Get logs for job 
            InstanceLogEntry[] logs = await client.GetAllJobInstanceLogsAsync(1602079200);

            Console.WriteLine($"found total of {logs.Length} logs:");
            Console.WriteLine(InstanceLogEntry.GetHeader());
            foreach (InstanceLogEntry log in logs)
                Console.WriteLine(log);

        }

        /// <summary>
        /// demo for GetJobInstanceLogs() with caching
        /// </summary>
        static async Task DemoLogsCached()
        {
            //get jobs, select first
            JobInstance[] jobs = await client.GetJobInstancesAsync(FilterItem.JobNameIs("Exchange02"),
                FilterItem.ReportStart(DateTime.Now.Subtract(TimeSpan.FromDays(2))),
                FilterItem.ReportEnd(DateTime.Now));

            JobInstance job = jobs[0];
            Stopwatch sw = new Stopwatch();
            Console.WriteLine($"running logs demo for job {job.DisplayName} ({job.ID})");

            //Get logs for job the first time
            Console.WriteLine("get all logs (first run; uncached)");
            sw.Restart();
            InstanceLogEntry[] logs = await job.GetLogEntriesAsync(getAllLogs: true);
            sw.Stop();

            Console.WriteLine($"found total of {logs.Length} logs in {sw.ElapsedMilliseconds} ms (1st; not cached)");

            // get logs the second time (cached)
            sw.Restart();
            logs = await job.GetLogEntriesAsync(getAllLogs: true);
            sw.Stop();

            Console.WriteLine($"found total of {logs.Length} logs in {sw.ElapsedMilliseconds} ms (2nd; cached)");
        }

        /// <summary>
        /// demo for GetVolsersUsed()
        /// </summary>
        /// <returns></returns>
        static async Task DemoVolsers()
        {
            //get jobs
            JobInstance[] jobs = await client.GetJobInstancesAsync(FilterItem.JobNameIs("ST010-NACL02_cifs_automotive"),
                FilterItem.ReportStart(DateTime.Now.Subtract(TimeSpan.FromDays(2))),
                FilterItem.ReportEnd(DateTime.Now));

            //list volsers for each found job
            Console.WriteLine($"Found {jobs.Length} jobs");
            foreach (JobInstance job in jobs)
            {
                if (job.RunType != JobRunType.BASE)
                    Console.WriteLine("not base job!");

                Console.WriteLine($"Volsers used by job {job.DisplayName}:");
                string[] volsers = await job.GetVolsersUsed();
                if (volsers == null)
                    Console.WriteLine("  none");
                else
                    foreach (string volser in volsers)
                        Console.WriteLine($"  {volser}");
            }
        }

        /// <summary>
        /// demo for GetBackupSize()
        /// </summary>
        static async Task DemoBackupSize()
        {
            //get jobs
            JobInstance[] jobs = await client.GetJobInstancesAsync(FilterItem.JobNameIs("ST010-NACL02_cifs_automotive"),
                FilterItem.ReportStart(DateTime.Now.Subtract(TimeSpan.FromDays(32))),
                FilterItem.ReportEnd(DateTime.Now));

            //list backup size for each found job
            Console.WriteLine($"Found {jobs.Length} jobs");
            foreach (JobInstance job in jobs)
            {
                Console.WriteLine($"Backup size info of job {job.DisplayName}:");
                DPXExtensions.JobSizeInfo size = await job.GetBackupSizeAsync();
                if (size == null)
                    Console.WriteLine("  no info available");
                else
                    Console.WriteLine($"  Total Data: {size.TotalDataBackedUp}; Total on Media: {size.TotalDataOnMedia}");
            }

        }

        /// <summary>
        /// demo for GetNodes() and GetNodeGroups()
        /// </summary>
        /// <returns></returns>
        static async Task DemoNodes()
        {
            //get all node groups
            NodeGroup[] groups = await client.GetNodeGroupsAsync();

            //print node grups
            Console.WriteLine($"found {groups.Length} node groups:");
            foreach (NodeGroup group in groups)
                Console.WriteLine($" {group.Name} - created by {group.Creator}");

            //get all nodes
            Node[] nodes = await client.GetNodesAsync();

            //print nodes
            Console.WriteLine($"found {nodes.Length} nodes:");
            foreach (Node node in nodes)
                Console.WriteLine($" {node.Name} in group {node.GroupName} running {node.OSDisplayName}");

            //get nodes in group
            Console.Write("enter node group name: ");
            string targetGroup = Console.ReadLine();
            Node[] targetedNodes = await client.GetNodesAsync(nodeGroup: targetGroup);

            //print nodes
            Console.WriteLine($"found {targetedNodes.Length} nodes:");
            foreach (Node node in targetedNodes)
                Console.WriteLine($" {node.Name} in group {node.GroupName} running {node.OSDisplayName}");
        }

        static void ConsoleWriteLineC(string msg, ConsoleColor fgc, bool useFGC)
        {
            ConsoleColor orgColor = Console.ForegroundColor;
            if (useFGC)
                Console.ForegroundColor = fgc;

            Console.WriteLine(msg);

            if (useFGC)
                Console.ForegroundColor = orgColor;
        }
    }
}
