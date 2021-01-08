using CommandLine;
using DPXTool.DPX.Model.License;
using DPXTool.Util;
using System;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode get-license
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Options for the get-license command
        /// </summary>
        [Verb("get-license", HelpText = "get license information")]
        class PrintLicenseOptions : BaseOptions
        {
            //no additional options needed
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
    }
}
