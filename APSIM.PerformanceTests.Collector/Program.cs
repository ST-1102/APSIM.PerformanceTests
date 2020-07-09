using Newtonsoft.Json;
using APSIM.PerformanceTests.Models;
using Models.Core;
using Models.Core.ApsimFile;
using Models.PostSimulationTools;
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
        static HttpClient httpClient = new HttpClient();
        static string LogFileName = "CsiroApsim";

        private static int retValue = 0;

        static int Main(string[] args)
        {
            try
            {
                string pullCmd = string.Empty;
                int pullId = 0;
                string submitDetails = string.Empty;
                DateTime runDate;
                string[] commandNames = new string[] { "AddToDatabase", "AddToDatabaseCSIRO", "Check" };  //can add to this over time


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

                    if (args.Length > 3)
                    {
                        submitDetails = args[3].ToString();
                    }

#if DEBUG
                    runDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);
                    runDate = runDate.AddDays(-1);
#endif

                    if (pullCmd == "AddToDatabaseCSIRO")
                    {
                        RetrieveHttpClientDetails("CsiroApsim");
                        LogFileName = "CsiroApsim";
                        pullCmd = "AddToDatabase";
                    }
                    else if (pullCmd == "AddToDatabaseAPSIM")
                    {
                        RetrieveHttpClientDetails("ApsimInfo");
                        LogFileName = "ApsimInfo";
                        pullCmd = "AddToDatabase";
                    }
                    else
                    {
                        RetrieveHttpClientDetails("CsiroApsim");
                        LogFileName = "CsiroApsim";
                    }

                    WriteToLogFile("  ");
                    WriteToLogFile("==========================================================");
                    WriteToLogFile(string.Format("Pull Request ID {0}, date {1}, command type: {2} ", pullId.ToString(), runDate.ToString("dd-MM-yyyy HH:mm"), pullCmd));

                    //(GET) Get all records back
                    //GetAllApsimFiles(httpclient).Wait();

                    //(GET) Get a single record back
                    //GetApsimFileByPullRequestID(httpclient, 2).Wait();



                    if (pullCmd == "AddToDatabase")
                    {
                        bool error = RetrieveData(pullId, runDate, submitDetails);
                        if (error)
                            retValue = 1;
                        pullCmd = "AddToDatabase";
                    }
                    //Console.ReadKey();      //this will pause the screen so that we can see the output in the console window
                }
            }
            catch (Exception ex)
            {
                retValue = 1;  // unhandled exception - set this to false
                WriteToLogFile("ERROR: " + ex.ToString());
            }
            return retValue;
        }

        private static void RetrieveHttpClientDetails(string type)
        {
            string serviceUrl = string.Empty; ;

            if (type == "CsiroApsim")
            {
                serviceUrl = ConfigurationManager.AppSettings["serviceAddress_csiro"].ToString() + "APSIM.PerformanceTests.Service/";
            }
            else if (type == "ApsimApsim")
            {
                serviceUrl = ConfigurationManager.AppSettings["serviceAddress_apsim"].ToString() + "APSIM.PerformanceTests.Service/";
            }
            else
            {
                serviceUrl = ConfigurationManager.AppSettings["serviceAddress_csiro"].ToString() + "APSIM.PerformanceTests.Service/";
            }
            //this is for apsimdev.apsim.info.au
            httpClient.BaseAddress = new Uri(serviceUrl);
#if DEBUG
            httpClient.BaseAddress = new Uri("http://localhost:53187/");
#endif

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.Timeout = TimeSpan.FromMinutes(10); // Allow for max 10 minutes to upload P/O data from a single apsim file.
        }

        /// <summary>
        /// Gets all of the ApsimFiles from the WebAPI
        /// </summary>
        /// <param name="cons"></param>
        /// <returns></returns>
        private static async Task GetAllApsimFiles(HttpClient cons)
        {
            HttpResponseMessage response = await cons.GetAsync("api/apsimfiles");
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                List<ApsimFile> apsimfiles = await response.Content.ReadAsAsync<List<ApsimFile>>();
                foreach (ApsimFile apsim in apsimfiles)
                {
                    WriteToLogFile(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", apsim.ID, apsim.PullRequestId, apsim.FileName, apsim.FullFileName, apsim.RunDate, apsim.IsMerged));
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
                WriteToLogFile(string.Format("{0}\t{1}\t{2}\t{3}\t{4}", apsim.PullRequestId, apsim.FileName, apsim.FullFileName, apsim.RunDate, apsim.IsMerged));

            }
        }
 


        /// <summary>
        /// 
        /// </summary>
        /// <param name="apsimInstance"></param>
        static async Task PostApsimRun(ApsimFile apsimInstance)
        {
            WriteToLogFile(string.Format("    Calling httpClient with ApsimFile {0}", apsimInstance.FileName));
            string apsimFileName = string.Empty;
            apsimFileName = apsimInstance.FileName;
            try
            {
                string json = JsonConvert.SerializeObject(apsimInstance);
                //this will call the service on www..apsim.info.au
                HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/apsimfiles", apsimInstance);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                    WriteToLogFile(string.Format("    Successfully posted ApsimFile {0}", apsimFileName));
                else
                    WriteToLogFile(string.Format("    ERROR posting ApsimFile {0}: {1}", apsimFileName, response.StatusCode.ToString()));
            }
            catch (TaskCanceledException ex)
            {
                string errorMessage = $"    ERROR posting Apsim File {apsimFileName} to Web API. ";
                if (!ex.CancellationToken.IsCancellationRequested)
                    errorMessage += "This is probably due to a timeout: ";
                errorMessage += ex.ToString();

                WriteToLogFile(errorMessage);
                throw ex;
            }
            catch (Exception ex)
            {
                WriteToLogFile(string.Format("    ERROR posting Apsim File {0} to Web API: {1} ", apsimFileName, ex.ToString()));
                throw ex;
            }
        }


        static async Task UpdatePullRequestsPassedTestsStatus(int id)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("api/acceptstats/" + id);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    WriteToLogFile(string.Format("    Successfully processed PassedTests Status for Pull Request Id: {0}", id));
                }

            }
            catch (Exception ex)
            {
                WriteToLogFile(string.Format("  ERROR:  Unable to process PassedTests Status for Pull Request Id {0}: {1}", id, ex.ToString()));
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
            Console.WriteLine("  4. (string) UserID");
            Console.WriteLine("  Example: APSIM.PerformanceTests.Collector.exe AddToDatabase 1111 2016.12.01-06:33 JHN321");

        }

        /// <summary>
        /// THIS IS THE MAIN FUNCTION WITHIN THIS PROGRAM
        /// Retreieves all Apsimx simulation files with for the search directory specified in the App.config file
        /// and then process these files.
        /// 
        /// Returns true iff an error is encountered.
        /// </summary>
        /// <param name="pullId"></param>
        /// <param name="runDate"></param>
        /// <param name="submitDetails"></param>
        private static bool RetrieveData(int pullId, DateTime runDate, string submitDetails)
        {
            bool error = false;
            //"C:/Jenkins/workspace/1. GitHub pull request/ApsimX/Tests/C:/Jenkins/workspace/1. GitHub pull request/ApsimX/Prototypes/";

            //need to allow for "Tests" and "ProtoTypes" directory
            //searchDir = @"C:/Users/cla473/Dropbox/APSIMInitiative/ApsimX/Tests/;C:/Users/cla473/Dropbox/APSIMInitiative/ApsimX/Prototypes/;";

            string searchDir = ConfigurationManager.AppSettings["searchDirectory"].ToString();
#if DEBUG
            searchDir = "C:/ApsimX/Tests/UnderReview/Chickpea";
#endif

            string[] filePaths = searchDir.Split(';');
            
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
                        //We don't need to save the full pathing here
                        WriteToLogFile("--------------------------------");
                        WriteToLogFile(string.Format("Apsimx file {0} found, Pull Request Id {1}, dated {2}", fi.FullName, pullId, runDate));

                        ApsimFile apsimFile = new ApsimFile();
                        apsimFile.FullFileName = fi.FullName;
                        apsimFile.FileName = Path.GetFileNameWithoutExtension(fi.FullName);
                        apsimFile.PullRequestId = pullId;
                        apsimFile.RunDate = runDate;

                        apsimFile.PredictedObserved = GetPredictedObservedDetails(fi.FullName);
                        apsimFile.SubmitDetails = submitDetails;

                        if (apsimFile.PredictedObserved.Count() > 0)
                        {
                            apsimFile.Simulations = GetSimulationDataTable(apsimFile.FileName, apsimFile.FullFileName);

                            try
                            {
                                PostApsimRun(apsimFile).Wait();
                            }
                            catch (Exception ex)
                            {
                                error = true;
                                WriteToLogFile(string.Format("    ERROR Posting Apsim File: {0}, Pull Request Id {1}, dated {2}: {3}", apsimFile.FileName, pullId, runDate,  ex.ToString()));
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        error = true;
                        WriteToLogFile(ex.ToString());
                    }

                }
            }

#if DEBUG
            //We don't need to call Gitub if we are in debug mode (and running local)
            //LogFileName = "CsiroApsim";
#endif
            //Call the Service to check the status of the Pull Request, (and subsequently call/update GitHub)
            //if (LogFileName != "CsiroApsim")
            //{
                UpdatePullRequestsPassedTestsStatus(pullId).Wait();
            //}
            return error;
        }

        /// <summary>
        /// Searches the specified file and returns all instances of PredictedObserved data.
        /// </summary>
        /// <param name="fileName">Path to the .apsimx file to be searched.</param>
        private static List<PredictedObservedDetails> GetPredictedObservedDetails(string fullFileName)
        {
            List<Exception> errors;
            // note - if we get a badformat exception thrown here, it's because .net is trying to
            // load a 64-bit version of sqlite3.dll for some reason. To fix this, we need to
            // copy the 32-bit version from ApsimX/DeploymentSupport/Windows/Bin/sqlite3.dll to
            // APSIM.PerformanceTests.Collector/Bin/Debug/ (or release if building in release mode).
            Simulations sims = FileFormat.ReadFromFile<Simulations>(fullFileName, out errors);
            if (errors != null && errors.Count > 0)
            {
                // Write all errors except for the last to a log file, and throw the last error
                // to ensure that we don't proceed further.
                for (int i = 0; i < errors.Count; i++)
                {
                    if (i == errors.Count - 1)
                        throw errors[i];
                    WriteToLogFile(string.Format("    ERROR opening file {0}: {1}", fullFileName, errors[i].ToString()));
                }
            }

            List<PredictedObservedDetails> predictedObservedDetailList = new List<PredictedObservedDetails>();
            foreach (PredictedObserved poModel in Apsim.ChildrenRecursively(sims, typeof(PredictedObserved)))
            {
                PredictedObservedDetails instance = new PredictedObservedDetails()
                {
                    DatabaseTableName = poModel.Name,
                    PredictedTableName = poModel.PredictedTableName,
                    ObservedTableName = poModel.ObservedTableName,
                    FieldNameUsedForMatch = poModel.FieldNameUsedForMatch,
                    FieldName2UsedForMatch = poModel.FieldName2UsedForMatch ?? string.Empty,
                    FieldName3UsedForMatch = poModel.FieldName3UsedForMatch ?? string.Empty,

                };
                instance.Data = GetPredictedObservedDataTable(poModel.Name, Path.ChangeExtension(fullFileName, ".db"));

                // Only add this instance if there is data.
                if ((instance.Data != null) && (instance.Data.Rows.Count > 0))
                    predictedObservedDetailList.Add(instance);
                else
                    WriteToLogFile(string.Format("    No PredictedObserved data was found for table {0} of file {1}", poModel.Name, fullFileName));
            }

            return predictedObservedDetailList;
        }

        /// <summary>
        /// Based on a specified PredictedObserved item, searches for the corresponding sqlite db file and extracts the
        /// datatable information for this PredictedObserved item.
        /// </summary>
        /// <param name="predictedObservedName">Name of the PredictedObserved table.</param>
        /// <param name="databasePath">Path to the .db file.</param>
        private static DataTable GetPredictedObservedDataTable(string predictedObservedName, string databasePath)
        {
            DataTable POdata = new DataTable(predictedObservedName);
            try
            {
                if (File.Exists(databasePath))
                {
                    string dbFileName = Path.GetFileName(databasePath);
                    using (SQLiteConnection con = new SQLiteConnection("Data Source=" + databasePath))
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
                                WriteToLogFile(string.Format("    There are {0} Predicted Observed records in database {1}, table {2}.", POdata.Rows.Count, dbFileName, predictedObservedName));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.ToString().IndexOf("no such table") > 0)
                            {
                                WriteToLogFile(string.Format("    For Database {0}, Table {1} does not exist: {2}", dbFileName, predictedObservedName, ex.ToString()));
                            }
                            else
                            {
                                WriteToLogFile(string.Format("    ERROR reading database {0}: {1}!", dbFileName, ex.ToString()));
                            }
                        }
                    }
                }
                else
                {
                     throw new Exception(string.Format("ERROR Database file {0} does not exist", databasePath));
                }

                string ColumnName, ObservedName, PredictedName;
                bool removeCols = false, colRemoved = false;
                int cIndex;

                //modLMC - 31-Jan-2018 - (as instructed by Dean) if the Predicted column is defined as String, then remove both Predicted & Observed
                for (int i = POdata.Columns.Count - 1; i >= 0; i--)
                {
                    ColumnName = POdata.Columns[i].ColumnName.Trim();
                    //modLMC - 20/02/2018 - as per phone conversation with Dean, remove any columns with CheckpointID
                    if (ColumnName.IndexOf("CheckpointID")  >= 0)
                    {
                        //Remove any columns called CheckpointID
                        POdata.Columns.RemoveAt(i);
                        //WriteToLogFile(String.Format("        NOTE: {0}.{1} was dropped.");
                        //i--;
                    }
                    else if (ColumnName.StartsWith("Predicted"))
                    {
                        //if datatype is not numeric need to remove it, and its corresponding observed column
                        colRemoved = false;

                        //check if the "Observed" Column exists, if it doesn't, then delete the Predicted
                        ObservedName = ColumnName.Replace("Predicted", "Observed");
                        try
                        {
                            cIndex = POdata.Columns[ObservedName].Ordinal;
                        }
                        catch (System.NullReferenceException)
                        {
                            POdata.Columns.RemoveAt(i);
                            WriteToLogFile(String.Format("        NOTE: {0}.{1} was dropped the Observed column {2} does not exist.", POdata.TableName, ColumnName, ObservedName));
                            colRemoved = true;
                            //i--;
                        }

                        if (colRemoved == false)
                        {
                            removeCols = false;
                            if (POdata.Columns[i].DataType == typeof(DateTime)) { removeCols = true; }
                            if (POdata.Columns[i].DataType == typeof(System.String)) { removeCols = true; }
                            if (removeCols == true)
                            {
                                WriteToLogFile(String.Format("        NOTE: {0}.{1} dropped as not in correct Format, was of Type {2} is not the correct; it should be a numeric column", POdata.TableName, ColumnName, POdata.Columns[i].DataType));
                                POdata.Columns.RemoveAt(i);
                                ObservedName = ColumnName.Replace("Predicted", "Observed");
                                try
                                {
                                    cIndex = POdata.Columns[ObservedName].Ordinal;
                                    if (cIndex > 0)
                                    {
                                        POdata.Columns.RemoveAt(cIndex);
                                        WriteToLogFile(String.Format("        NOTE: {0}.{1} was also dropped as {2} was not defined as a numeric column", POdata.TableName, ObservedName, ColumnName));
                                        if (i >= POdata.Columns.Count)
                                        {
                                            //make sure we don't go out of bounds with the columns
                                            i = POdata.Columns.Count;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }

                    }
                    else if (ColumnName.StartsWith("Observed"))
                    {
                        //check if the "Predicted" Column exists, if it doesn't, then delete the Predicted
                        PredictedName = ColumnName.Replace("Observed", "Predicted");
                        try
                        {
                            cIndex = POdata.Columns[PredictedName].Ordinal;
                        }
                        catch (System.NullReferenceException)
                        {
                            POdata.Columns.RemoveAt(i);
                            WriteToLogFile(String.Format("        NOTE: {0}.{1} was dropped the Predicted column {2} does not exist.", POdata.TableName, ColumnName, PredictedName));
                            //i--;
                        }
                    }

                }

                POdata.AcceptChanges();
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
                        else if ((POdata.Columns[i].DataType != typeof(System.Double)) && (POdata.Columns[i].DataType != typeof(System.Int64)))
                        {
                            try
                            {
                                //Update the log file to report incorrect data types
                                WriteToLogFile(String.Format("        NOTE: {0}.{1} Format Type {2} is not the correct; it should be a numeric column", POdata.TableName, ColumnName, POdata.Columns[i].DataType));

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
                                WriteToLogFile(String.Format("        ERROR:  Unable to convert {0}.{1} to double: {2}", POdata.TableName, ColumnName, ex.ToString()));
                            }
                        }
                        
                    }
                }
                return POdata;

            }
            catch (Exception ex)
            {
                WriteToLogFile("    ERROR:  Unable to access Data: " + ex.ToString());
                return POdata;
            }
        }

        /// <summary>
        /// Returns the Simulation dataTable from the SQLite database for a specific PredictedObserved TableName
        /// </summary>
        /// <param name="predictedObservedName"></param>
        /// <param name="databasePath"></param>
        /// <returns></returns>
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
                        string selectSQL = "SELECT ID, Name FROM _Simulations ";

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
            catch (ConstraintException err)
            {
                WriteToLogFile("    Unable to retrieve DataTable: " + err.ToString());
                if (simData != null && simData.HasErrors)
                {
                    string[] columnNames = simData.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                    foreach (DataRow error in simData.GetErrors())
                    {
                        StringBuilder info = new StringBuilder();
                        info.AppendLine("Additional info:");
                        for (int i = 0; i < columnNames.Length; i++)
                            info.AppendLine($"{columnNames[i]}: {error.ItemArray[i]}");

                        WriteToLogFile(error.RowError);
                        WriteToLogFile(info.ToString());
                    }
                }
                retValue = 1;
                return simData;
            }
            catch (Exception ex)
            {
                WriteToLogFile("    Unable to retrieve DataTable: " + ex.ToString());
                retValue = 1;
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
                if (message.ToString().Trim().IndexOf("ERROR") > 0)
                {
                    Console.WriteLine(message.ToString().Trim());
                }

                //Need to make sure we are in the same directory as this application 
                string fileName = getDirectoryPath("PerformanceCollector.txt");
                //if (LogFileName == "CsiroApsim")
                //{
                //    fileName = getDirectoryPath("PerformanceCollectorCSIRO.txt");
                //}
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
#if DEBUG
            returnStr = "C:\\ApsimWork\\" + fileName; 
#endif
            return returnStr;
        }
    }

}
