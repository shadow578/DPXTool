using CommandLine;
using DPXTool.DPX.Model.Nodes;
using DPXTool.Util;
using System;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode get-node-groups
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Options for the get-node-groups command
        /// </summary>
        [Verb("get-node-groups", HelpText = "get a list of all node groups")]
        class GetNodeGroupsOptions : BaseOptions
        {
            //no additional options needed
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
                w.WriteRow(g.Name, g.Comment, g.MediaPool, g.ClusterName, g.Creator, g.CreationTime.ToString(DATETIME_FORMAT));

            //write table
            Console.WriteLine($"Found {groups.Length} node groups:");
            if (!options.NoPrintToConsole)
                w.WriteToConsole();
            await WriteTableToFile(options, w);
        }
    }
}
