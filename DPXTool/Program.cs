﻿using DPXTool.DPX;
using DPXTool.DPX.Model.Common;
using DPXTool.DPX.Model.Constants;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.DPX.Model.License;
using System;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// contains early usage demos and testts for the DPXClient class.
    /// Just staying here as a reference, may be removed later :P
    /// </summary>
    [Obsolete("early usage demo and tests for DPX Client")]
    class Program
    {

        static void MainX(string[] args)
        {
            test().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static DPXClient client;

        static async Task test()
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
            pw = null;

            //demos
            await DemoLicense();
            await DemoJobs();
            await DemoLogs();
            await DemoVolsers();

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
        /// demo for GetVolsersUsed()
        /// </summary>
        /// <returns></returns>
        static async Task DemoVolsers()
        {
            //get jobs
            JobInstance[] jobs = await client.GetJobInstancesAsync(FilterItem.JobNameIs("ST010-NACL02_cifs_automotive"),
                FilterItem.ReportStart(new DateTime(2020, 9, 1)),
                FilterItem.ReportEnd(new DateTime(2020, 10, 1)));

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
