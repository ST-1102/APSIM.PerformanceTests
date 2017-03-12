﻿using APSIM.PerformanceTests.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace APSIM.PerformanceTests.Collector
{
    class Program
    {
        static HttpClient httpclient = new HttpClient();

        static int Main(string[] args)
        {

            httpclient.BaseAddress = new Uri("http://www.apsim.info/APSIM.PerformanceTests.Service/");
            //httpclient.BaseAddress = new Uri("http://localhost:53187/");
            httpclient.DefaultRequestHeaders.Accept.Clear();
            httpclient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


            //initially set to true
            int retValue = 1;
            //Console.Title = typeof(Program).Name;
            string pullCmd = string.Empty;
            int pullId = 0;
            DateTime runDate;
            string[] commandNames = new string[] { "AddToDatabase", "Check" };  //can add to this over time

            try
            {

                //Test that something has been passed
                if (args.Length == 0)
                {
                    throw new Exception("The command type, the Date and/or the GitHub Pull Request Id are missing!");
                }
                else if (args.Length == 1 & HelpRequired(args[0]))
                {
                    DisplayHelp();
                }
                else
                {
                    pullCmd = args[0];
                    if (!commandNames.Contains(pullCmd))
                    {
                        throw new Exception(string.Format("ABORTED!  Invalid command passed: {0}.", pullCmd));
                    }
                    pullId = Int32.Parse(args[1]);
                    runDate = DateTime.ParseExact(args[2], "yyyy.MM.dd-HH:mm", CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal);

                    WriteToLogFile("-------------------------------------------------------------------------------");
                    WriteToLogFile(string.Format("Pull Request ID {0}, date {1}, command type: {2} ", pullId.ToString(), runDate.ToString("dd-MM-yyyy HH:mm"), pullCmd));


                    //(GET) Get all records back
                    //GetAllApsimFiles(httpclient).Wait();

                    //(GET) Get a single record back
                    //GetApsimFileByPullRequestID(httpclient, 2).Wait();

                    //(PUT) Update a single record
                    //UpdateApsimFileRequestName(httpclient, 2, "New").Wait();



                    if (pullCmd == "AddToDatabase")
                    {
                        RetrieveData(pullId, runDate);
                    }
                    //Console.ReadKey();      //this will pause the screen so that we can see the output in the console window
                }
            }
            catch (Exception ex)
            {
                retValue = 0;  // unhandled exception - set this to false
                WriteToLogFile(" ERROR: " + ex.Message.ToString());
            }
            return retValue;
        }


        private static async Task GetAllApsimFiles(HttpClient cons)
        {
            HttpResponseMessage response = await cons.GetAsync("api/apsimfiles");
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                List<ApsimFile> apsimfiles = await response.Content.ReadAsAsync<List<ApsimFile>>();
                foreach (ApsimFile apsim in apsimfiles)
                {
                    WriteToLogFile(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", apsim.ID, apsim.PullRequestId, apsim.FileName, apsim.FullFileName, apsim.RunDate, apsim.IsReleased));
                }
               // Console.ReadLine();

            }
        }

        //ULTIMATELY, THIS WILL RETURN MULTIPLE, BUT FOR NOW, JUST LEAVE IT AS ONE
        private static async Task GetApsimFileByPullRequestID(HttpClient cons, int id)
        {
            HttpResponseMessage response = await cons.GetAsync("api/apsimfiles/" + id);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                ApsimFile apsim = await response.Content.ReadAsAsync<ApsimFile>();
                WriteToLogFile(string.Format("{0}\t{1}\t{2}\t{3}\t{4}", apsim.PullRequestId, apsim.FileName, apsim.FullFileName, apsim.RunDate, apsim.IsReleased));

            }
        }

        private static async Task UpdateApsimFileRequestName(HttpClient cons, int id, bool isReleased)
        {
            HttpResponseMessage response = await cons.GetAsync("api/apsimfiles/" + id);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                //ApsimFile apsim = await response.Content.ReadAsAsync<ApsimFile>();
                //apsim.FullFileName = newName + " " +  apsim.FullFileName;
                response = await cons.PutAsJsonAsync("api/apsimfiles/" + id, isReleased);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    WriteToLogFile(string.Format("Pull Request Id {0} updated isReleased = {1}", id, isReleased));
                }
            }
        }


 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="apsimInstance"></param>
        static async Task PostApsimRun(HttpClient con, ApsimFile apsimInstance)
        {
            string apsimFileName = apsimInstance.FileName;
            try
            {
                HttpResponseMessage response = await con.PostAsJsonAsync("api/apsimfiles", apsimInstance);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    WriteToLogFile(string.Format("    Successfully Posted Apsim File Information {0}", apsimFileName));
                }
            }
            catch (Exception ex)
            {
                WriteToLogFile(string.Format("    ERROR posting Apsim File {0} to Web API: {2} ", apsimFileName, ex.Message.ToString()));
            }
        }


        private static bool HelpRequired(string param)
        {
            return param == "-h" || param == "--help" || param == "/?";
        }

        private static void DisplayHelp()
        {

            Console.WriteLine("Parameters required are: ");
            Console.WriteLine("  1. (string) Command Type");
            Console.WriteLine("  2. (int) Pull Request Id");
            Console.WriteLine("  3. (datetime) Date");
            Console.WriteLine("  Example: APSIM.PerformanceTests.Collector.exe AddToDatabase 1111 2016.12.01-06:33");

        }

        /// <summary>
        /// Retreieves all Apsimx simulation files with for the search directory specified in the App.config file
        /// and then process these files
        /// </summary>
        /// <param name="pullId"></param>
        private static void RetrieveData(int pullId, DateTime runDate)
        {
            //filePath = "C:/Users/cla473/Dropbox/APSIMInitiative/ApsimX/Tests/";
            //filePath = "\\scoop.apsim.info\c$\Jenkins\workspace/1. GitHub pull request/ApsimX/Tests/";
            //List<ApsimFile> apsimFiles = new List<ApsimFile>();

            //need to allow for "Tests" and "ProtoTypes" directory
            string[] filePaths = ConfigurationManager.AppSettings["searchDirectory"].ToString().Split(';');

            foreach (string filePath in filePaths)
            {
                string currentPath = filePath.Trim();
                DirectoryInfo info = new DirectoryInfo(@currentPath);
                //FileInfo[] files = info.GetFiles("*.apsimx", SearchOption.AllDirectories).Where(p => p.CreationTime >= runDate).OrderBy(p => p.CreationTime).ToArray();
                FileInfo[] files = info.GetFiles("*.apsimx", SearchOption.AllDirectories).ToArray();
                foreach (FileInfo fi in files)
                {
                    try
                    {
                        WriteToLogFile(string.Format("Apsimx file {0} found, Pull Request Id {1}, dated {2}", fi.FullName, pullId, runDate));
                        ApsimFile apsimFile = new ApsimFile();
                        apsimFile.FullFileName = fi.FullName;
                        apsimFile.FileName = Path.GetFileNameWithoutExtension(fi.FullName);

                        apsimFile.PredictedObserved = GetPredictedObservedDetails(fi.FullName);

                        if (apsimFile.PredictedObserved.Count() > 0)
                        //if (PredictedObservedDatabaseDataExists == true)
                        {
                            apsimFile.Simulations = GetSimulationDataTable(apsimFile.FileName, apsimFile.FullFileName);
                            //apsimFiles.Add(apsimFile);
                            apsimFile.PullRequestId = pullId;
                            apsimFile.RunDate = runDate;

                            try
                            {
                                PostApsimRun(httpclient, apsimFile).Wait();
                            }
                            catch (Exception ex)
                            {
                                WriteToLogFile(string.Format("     ERROR Posting Apsim File: {0}, Pull Request Id {1}, dated {2}: {3}", apsimFile.FileName, pullId, runDate,  ex.Message.ToString()));
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToLogFile(ex.Message);
                    }
                }
            }
            //return apsimFiles;
        }

        /// <summary>
        /// Searches the specified file and searches for all instances of PredictedObserved data, and adds it to a list of PredictedObserved items.
        /// </summary>
        /// <param name="fileName"></param>
        private static List<PredictedObservedDetails> GetPredictedObservedDetails(string fullFileName)
        {

            //PredictedObservedDetails[] predictedObservedDetailList = new PredictedObservedDetails[0];
            List<PredictedObservedDetails> predictedObservedDetailList = new List<PredictedObservedDetails>();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(fullFileName);


            //these are under simulation/area/outputfile/filename, title)
            XmlNodeList elemList = xmlDoc.SelectNodes("//PredictedObserved");
            if (elemList.Count > 0)
            {
                //predictedObservedDetailList = new PredictedObservedDetails[elemList.Count];
                for (int i = 0; i < elemList.Count; i++)
                {
                    PredictedObservedDetails PredictedObservedInstance = new PredictedObservedDetails();
                    PredictedObservedInstance.FieldName2UsedForMatch = string.Empty;
                    PredictedObservedInstance.FieldName3UsedForMatch = string.Empty;

                    bool hasData = false;
                    if (elemList[i].HasChildNodes) { hasData = true; }
                    if (elemList[i].ParentNode.Name != "DataStore") { hasData = false; }
                    if (hasData == true)
                    {
                        for (int n = 0; n < elemList[i].ChildNodes.Count; n++)
                        {
                            string nodeName = elemList[i].ChildNodes[n].Name;
                            switch (nodeName)
                            {
                                case "Name":
                                    PredictedObservedInstance.DatabaseTableName = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                //case "Tests":
                                //    PredictedObservedInstance.Tests = elemList[i].ChildNodes[n].InnerText;
                                //    break;
                                case "PredictedTableName":
                                    PredictedObservedInstance.PredictedTableName = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                case "ObservedTableName":
                                    PredictedObservedInstance.ObservedTableName = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                case "FieldNameUsedForMatch":
                                    PredictedObservedInstance.FieldNameUsedForMatch = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                case "FieldName2UsedForMatch":
                                    PredictedObservedInstance.FieldName2UsedForMatch = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                case "FieldName3UsedForMatch":
                                    PredictedObservedInstance.FieldName3UsedForMatch = elemList[i].ChildNodes[n].InnerText;
                                    break;
                                    //WHAT IF THE MATCH IS DONE ACROSS MULTIPLE FIELDS (TWO OR THREE).
                            }
                        }
                        PredictedObservedInstance.PredictedObservedData = GetPredictedObservedDataTable(PredictedObservedInstance.DatabaseTableName, fullFileName);
                    }
                    //only add this instance if there is data
                    if ((PredictedObservedInstance.PredictedObservedData != null) && (PredictedObservedInstance.PredictedObservedData.Rows.Count > 0))
                    {
                        predictedObservedDetailList.Add(PredictedObservedInstance);
                    }
                }
            }
            return predictedObservedDetailList;
        }

        /// <summary>
        /// Based on a specified PredictedObserved item, searches for the corresponding sqlite db file and extracts the
        /// datatable information for this PredictedObserved item
        /// </summary>
        /// <param name="apsimRun"></param>
        /// <returns></returns>
        private static DataTable GetPredictedObservedDataTable(string predictedObservedName, string databasePath)
        {
            DataTable POdata = new DataTable(predictedObservedName);
            try
            {
                string dbName = Path.GetFileNameWithoutExtension(databasePath) + ".db";
                string fullPath = Path.GetDirectoryName(databasePath) + "\\" + dbName;
                if (File.Exists(string.Format(@"{0}", fullPath)))
                {
                    using (SQLiteConnection con = new SQLiteConnection("Data Source=" + fullPath))
                    {
                        con.Open();
                        string selectSQL = "SELECT * FROM " + predictedObservedName;
                        SQLiteCommand cmd = new SQLiteCommand(selectSQL, con);
                        try
                        {
                            SQLiteDataReader rdr = cmd.ExecuteReader();
                            if (rdr != null)
                            {
                                POdata.Load(rdr);
                                WriteToLogFile(string.Format("    There are {0} Predicted Observed records in database {1}, table {2}.", POdata.Rows.Count, dbName, predictedObservedName));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.ToString().IndexOf("no such table") > 0)
                            {
                                WriteToLogFile(string.Format("    For Database {0}, Table {1} does not exist!", dbName, predictedObservedName));
                            }
                            else
                            {
                                WriteToLogFile(string.Format("    ERROR reading database {0}: {1}!", dbName, ex.Message.ToString()));
                            }
                        }
                    }
                }
                else
                {
                     throw new Exception(string.Format("Database {0} does not exist", dbName));
                }

                string ColumnName;

                //need to ensure that we can convert test/string/char columns to real for all Predicted and Observed Columns
                //need to work backwards, just in case we need to delete any columns
                for (int i = POdata.Columns.Count-1; i >= 0 ; i--)
                {
                    ColumnName = POdata.Columns[i].ColumnName.Trim();
                    if (ColumnName.StartsWith("Observed") || ColumnName.StartsWith("Predicted"))
                    {
                        if (POdata.Columns[i].DataType == typeof(DateTime))
                        {
                            POdata.Columns.RemoveAt(i);
                        }
                        else if (POdata.Columns[i].DataType != typeof(System.Double))
                        {
                            try
                            {
                                //this will error on null values
                                //DataTable dtCloned = POdata.Clone();
                                //dtCloned.Columns[i].DataType = typeof(System.Double);
                                //foreach (DataRow row in POdata.Rows)
                                //{
                                //    dtCloned.ImportRow(row);
                                //}
                                //POdata = dtCloned;
                                //WriteToLogFile(String.Format("ERROR: Datatable {0} column {1} Format Type {2} is not the correct; it should be a numeric column", POdata.TableName, ColumnName, POdata.Columns[i].DataType));

                                ////rename the original
                                string origCol = "orig" + ColumnName;
                                POdata.Columns[i].ColumnName = origCol;
                                //create a new column, with the correct type
                                POdata.Columns.Add(ColumnName, typeof(System.Double));
                                for (int ri = 0; ri < POdata.Rows.Count; ri++)
                                {
                                    double value;
                                    if (double.TryParse(POdata.Rows[ri][i].ToString(), out value))
                                    {
                                        POdata.Rows[ri][ColumnName] = value;
                                    }
                                    else
                                    {
                                        POdata.Rows[ri][ColumnName] = DBNull.Value;
                                    }
                                }
                                //now remove the original column
                                POdata.Columns.RemoveAt(i);
                            }
                            catch (Exception ex)
                            {
                                WriteToLogFile(String.Format("ERROR:  Unable to convert datatable {0} column {1} to double: {2}", POdata.TableName, ColumnName, ex.Message.ToString()));
                            }
                        }
                        
                    }
                }
                return POdata;

            }
            catch (Exception ex)
            {
                WriteToLogFile("    ERROR:  Unable to access Data: " + ex.Message.ToString());
                return POdata;
            }
        }

        private static DataTable GetSimulationDataTable(string predictedObservedName, string databasePath)
        {
            DataTable simData = new DataTable("Simulations");
            string dbName = Path.GetFileNameWithoutExtension(databasePath) + ".db";
            try
            {
                string fullPath = Path.GetDirectoryName(databasePath) + "\\" + Path.GetFileNameWithoutExtension(databasePath) + ".db";
                if (File.Exists(string.Format(@"{0}", fullPath)))
                {
                    using (SQLiteConnection con = new SQLiteConnection("Data Source=" + fullPath))
                    {
                        con.Open();
                        string selectSQL = "SELECT * FROM Simulations ";

                        using (SQLiteCommand cmd = new SQLiteCommand(selectSQL, con))
                        {
                            using (SQLiteDataReader rdr = cmd.ExecuteReader())
                            {
                                simData.Load(rdr);
                                WriteToLogFile(string.Format("    There are {0} Simulation records in the database {1}.", simData.Rows.Count, dbName));
                                return simData;
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception(string.Format("Database {0}.db does not exist", predictedObservedName));
                }

            }
            catch (Exception ex)
            {
                WriteToLogFile("    Unable to retrieve DataTable: " + ex.Message.ToString());
                return simData;
            }
        }



        /// <summary>
        /// Writes information to the log file
        /// </summary>
        /// <param name="message"></param>
        private static void WriteToLogFile(string message)
        {
            if (message.Length > 0)
            {
                //this is just a temporary measure so that I can see what is happening
                //Console.WriteLine(message);

                //Need to make sure we are in the same directory as this application 
                string fileName = getDirectoryPath("PerformanceTestsLog.txt");
                StreamWriter sw;

                if (!File.Exists(fileName))
                {
                    sw = new StreamWriter(fileName);
                }
                else
                {
                    sw = File.AppendText(fileName);
                }
                //string logLine = String.Format("{0:G}: {1}.", System.DateTime.Now, message);
                string logLine = String.Format("{0}: {1}", System.DateTime.Now.ToString("yyyy-MMM-dd HH:mm"), message);

                sw.WriteLine(logLine);
                sw.Close();
            }
        }

        /// <summary>
        /// creates the file/name path details for the for the specified file and the application's path.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static string getDirectoryPath(string fileName)
        {
            string returnStr = string.Empty;

            //To get the location the assembly normally resides on disk or the install directory
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

            returnStr = Path.GetDirectoryName(path) + "\\" + fileName;
            return returnStr;
        }
    }

}
