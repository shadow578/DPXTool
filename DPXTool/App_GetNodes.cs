using CommandLine;
using DPXLib.Model.Nodes;
using DPXTool.Util;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace DPXTool
{
    /// <summary>
    /// Main Application class, parts:
    /// - AppMode get-nodes
    /// </summary>
    public partial class App
    {
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
                w.WriteRow(n.GroupName, n.Name, n.ServerName, n.Type, n.OSGroup.ToString(), n.OSDisplayName, n.OSVersion, n.Creator, n.CreationTime.ToString(DATETIME_FORMAT), n.Comment);


            //write table
            Console.WriteLine($"Found {nodes.Length} nodes:");

            if (!options.NoPrintToConsole)
                w.WriteToConsole();
            await WriteTableToFile(options, w);
        }
    }
}
