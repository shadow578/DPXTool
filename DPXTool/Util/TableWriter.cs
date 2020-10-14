using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPXTool.Util
{
    /// <summary>
    /// a writer that can write tables to csv and html files
    /// </summary>
    public class TableWriter
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
        /// internal table structure to print later
        /// </summary>
        List<string[]> table = new List<string[]>();

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="cells">the cells to write</param>
        public void WriteRow(params string[] cells)
        {
            //check lenght is right
            if (table.Count > 0 && table[0].Length != cells.Length)
                throw new InvalidDataException("Count of cells has to be the same for all rows of the table!");

            //add cells to table
            table.Add(cells);
        }

        /// <summary>
        /// write the table to the local console
        /// </summary>
        /// <param name="trunctateWidth">should the table width be limited to the console width if it does not fit?</param>
        /// <param name="autoPromptWidth">should the user be automatically prompted to adjust the console with to fit the table?</param>
        public void WriteToConsole(bool trunctateWidth = true, bool autoPromptWidth = true)
        {
            //check there is at least one row to write
            if (table.Count <= 0) return;

            //replace null strings with -
            for (int r = 0; r < table.Count; r++)
                for (int c = 0; c < table[r].Length; c++)
                    if (string.IsNullOrWhiteSpace(table[r][c]))
                        table[r][c] = "-";

            //pre- calculate width of each column
            int[] columnWidth = new int[table.First().Length];
            for (int row = 0; row < table.Count; row++)
                for (int column = 0; column < table[row].Length; column++)
                {
                    int colWidth = table[row][column].Length;
                    if (columnWidth[column] < colWidth)
                        columnWidth[column] = colWidth;
                }

            if (autoPromptWidth)
            {
                //calculate total table width
                int tableWidth = -3;
                foreach (int w in columnWidth)
                    tableWidth += w + 3;

                //limit table width to maximum window width
                if (tableWidth > Console.LargestWindowWidth)
                    tableWidth = Console.LargestWindowWidth;

                //check width matches current console, if not prompt for adjustment
                if (tableWidth >= Console.WindowWidth)
                {
                    Console.WriteLine("Please adjust window to fit indicator without line break, then press enter");
                    Console.Write("|");
                    for (int i = 0; i < tableWidth; i++)
                        Console.Write("-");
                    Console.WriteLine("|");
                    Console.ReadLine();
                }
            }

            //write table to console
            for (int row = 0; row < table.Count; row++)
            {
                //build row string
                StringBuilder rowB = new StringBuilder();
                for (int column = 0; column < table[row].Length; column++)
                    rowB.Append(Pad(table[row][column], columnWidth[column]) + " | ");

                //trunctate row string if needed
                if (rowB.Length >= Console.WindowWidth && trunctateWidth)
                    rowB.Length = Console.WindowWidth - 1;

                //write row
                Console.WriteLine(rowB.ToString());
            }
        }

        /// <summary>
        /// write the table to a file
        /// </summary>
        /// <param name="filePath">the file to write to</param>
        /// <param name="format">the format of the file</param>
        public async Task WriteToFileAsync(string filePath, TableFormat format)
        {
            //check there is at least one row to write
            if (table.Count <= 0) return;

            //create file
            using (StreamWriter w = File.CreateText(filePath))
            {
                //begin document
                if (format == TableFormat.CSV)
                    await BeginCSVDocumentAsync(w);
                else
                    await BeginHTMLDocumentAsync(w);

                //write table
                for (int r = 0; r < table.Count; r++)
                {
                    //replace null strings with -
                    for (int c = 0; c < table[r].Length; c++)
                        if (string.IsNullOrWhiteSpace(table[r][c]))
                            table[r][c] = "-";

                    //write each row
                    if (format == TableFormat.CSV)
                        await WriteCSVRowAsync(w, r == 0, table[r]);
                    else
                        await WriteHTMLRowAsync(w, r == 0, table[r]);
                }

                //end document
                if (format == TableFormat.CSV)
                    await EndCSVDocumentAsync(w);
                else
                    await EndHTMLDocumentAsync(w);

                //flush and close
                await w.FlushAsync();
            }
        }

        #region CSV
        /// <summary>
        /// begin writing the document
        /// </summary>
        /// <param name="fileOut">the writer to write to</param>
        async Task BeginCSVDocumentAsync(StreamWriter fileOut)
        {
            //no headers needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="isHeader">is this the first (header) row?</param>
        /// <param name="cells">the cells to write</param>
        /// <param name="fileOut">the writer to write to</param>
        async Task WriteCSVRowAsync(StreamWriter fileOut, bool isHeader, params string[] cells)
        {
            //build line (header rows are not different)
            StringBuilder csv = new StringBuilder();
            foreach (string cell in cells)
                csv.Append(cell).Append(";");

            //write to file
            await fileOut.WriteLineAsync(csv);
        }

        /// <summary>
        /// end writing the document and close it
        /// </summary>
        /// <param name="fileOut">the writer to write to</param>
        async Task EndCSVDocumentAsync(StreamWriter fileOut)
        {
            //no footer needed
            await Task.CompletedTask;
        }
        #endregion

        #region HTML
        /// <summary>
        /// begin writing the document
        /// </summary>
        /// <param name="fileOut">the writer to write to</param>
        async Task BeginHTMLDocumentAsync(StreamWriter fileOut)
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
            await fileOut.WriteAsync(html);
        }

        /// <summary>
        /// write a row of cells to the document
        /// </summary>
        /// <param name="isHeader">is this the first (header) row?</param>
        /// <param name="cells">the cells to write</param>
        /// <param name="fileOut">the writer to write to</param>
        async Task WriteHTMLRowAsync(StreamWriter fileOut, bool isHeader, params string[] cells)
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
            await fileOut.WriteAsync(html);
        }

        /// <summary>
        /// end writing the document and close it
        /// </summary>
        /// <param name="fileOut">the writer to write to</param>
        async Task EndHTMLDocumentAsync(StreamWriter fileOut)
        {
            string html = @"
<!--after-->
</table>
</body>
";
            await fileOut.WriteAsync(html);
        }
        #endregion

        /// <summary>
        /// pad a string to the defined lenght
        /// </summary>
        /// <param name="str">the string to pad</param>
        /// <param name="padToLength">the length to pad to</param>
        /// <param name="padChar">char to pad with</param>
        /// <returns>the padded string</returns>
        string Pad(string str, int padToLength, char padChar = ' ')
        {
            //check string needs padding
            if (str.Length >= padToLength)
                return str;

            //pad string
            StringBuilder b = new StringBuilder(str);
            for (int i = 0; i < (padToLength - str.Length); i++)
                b.Append(padChar);
            return b.ToString();
        }

    }
}
