using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DPXTool.Util
{
    /// <summary>
    /// a writer that can write tables to csv and html files
    /// </summary>
    public class TableWriter : IDisposable
    {
        /// <summary>
        /// the format of a table
        /// </summary>
        public enum TableFormat
        {
            CSV,
            HTML
        }

        /// <summary>
        /// The file to write to
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// the format to write in
        /// </summary>
        public TableFormat Format { get; private set; }

        /// <summary>
        /// writer that writes to the output file
        /// </summary>
        StreamWriter o;

        /// <summary>
        /// initialize the writer
        /// </summary>
        /// <param name="filepath">the file to write to</param>
        /// <param name="format">the format to write in</param>
        public TableWriter(string filepath, TableFormat format)
        {
            FilePath = filepath;
            Format = format;
        }

        /// <summary>
        /// begin writing the document
        /// </summary>
        public async Task BeginDocumentAsync()
        {
            //create file
            o = File.CreateText(FilePath);

            //call format function 
            if (Format == TableFormat.CSV)
                await BeginCSVDocumentAsync();
            else
                await BeginHTMLDocumentAsync();
        }

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="isHeader">is this the first (header) row?</param>
        /// <param name="cells">the cells to write</param>
        public async Task WriteRowAsync(bool isHeader = false, params string[] cells)
        {
            //check writer is ready
            if (o == null) return;

            //check at least one cell
            if (cells.Length <= 0) return;

            //call format function
            if (Format == TableFormat.CSV)
                await WriteCSVRowAsync(isHeader, cells);
            else
                await WriteHTMLRowAsync(isHeader, cells);
        }

        /// <summary>
        /// end writing the document and close it
        /// </summary>
        public async Task EndDocumentAsync()
        {
            //check writer is ready
            if (o == null) return;

            //call format function
            if (Format == TableFormat.CSV)
                await EndCSVDocumentAsync();
            else
                await EndHTMLDocumentAsync();

            //close document
            o?.Flush();
            o?.Close();
            o = null;
        }

        #region CSV
        /// <summary>
        /// begin writing the document
        /// </summary>
        async Task BeginCSVDocumentAsync()
        {
            //no headers needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="isHeader">is this the first (header) row?</param>
        /// <param name="cells">the cells to write</param>
        async Task WriteCSVRowAsync(bool isHeader, params string[] cells)
        {
            //build line (header rows are not different)
            StringBuilder csv = new StringBuilder();
            foreach (string cell in cells)
                csv.Append(cell).Append(";");

            //write to file
            await o.WriteLineAsync(csv);
        }

        /// <summary>
        /// end writing the document and close it
        /// </summary>
        async Task EndCSVDocumentAsync()
        {
            //no footer needed
            await Task.CompletedTask;
        }
        #endregion

        #region HTML
        /// <summary>
        /// begin writing the document
        /// </summary>
        async Task BeginHTMLDocumentAsync()
        {
            string html = @"
<!DOCTYPE html>
<head>
    <style>
        table, th, td {
            border: 1px solid black;
            border-collapse: collapse;
        }
        th, td {
            padding: 5px;
            text-align: center;
        }
        td {
            font-weight: 300;
        }
        tr {
            height: 28px;
        }
        table {
            width: 100%;
        }
        p {
            font-size: 3.5mm;
            font-weight: 300;
        }
    </style>
</head>
<body>
<table>
<!--before-->";
            await o.WriteAsync(html);
        }

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="isHeader">is this the first (header) row?</param>
        /// <param name="cells">the cells to write</param>
        async Task WriteHTMLRowAsync(bool isHeader, params string[] cells)
        {
            //get table data tag (th or td)
            string td = isHeader ? "th" : "td";

            //build table html
            StringBuilder html = new StringBuilder();
            html.AppendLine("<tr>");
            foreach (string cell in cells)
                html.AppendLine($"<{td}>{cell}</{td}>");
            html.AppendLine("</tr>");

            //write to file
            await o.WriteAsync(html);
        }

        /// <summary>
        /// end writing the document and close it
        /// </summary>
        async Task EndHTMLDocumentAsync()
        {
            string html = @"
<!--after-->
</table>
</body>
";
            await o.WriteAsync(html);
        }
        #endregion

        /// <summary>
        /// dispose the writer and close the file
        /// </summary>
        public void Dispose()
        {
            o?.Flush();
            o?.Dispose();
        }
    }
}
