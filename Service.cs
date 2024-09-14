using log4net;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static System.Collections.Specialized.BitVector32;
using System.Xml.Linq;
using System.Runtime.Remoting.Contexts;
using log4net.Util;
using System.Diagnostics.Eventing.Reader;
using System.Data.Entity.Migrations;


namespace EDI_KMC_Service
{
    public partial class Service : ServiceBase
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Timer timer = new Timer();

        public int intPurge = Properties.Settings.Default.PurgeLogFilesMonth;
        public string strStorageFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        public string strWorkFolder = "";
        public string strLogFolder = "";
        public int intCycleTimeMin = (int) Properties.Settings.Default.CycleTimeMinutes;


        public Service()
        {
            InitializeComponent();
            _log.Info("Inititalizing Service");
        }

        #region OnDebug
        public void OnDebug()
        {
            OnStart(null);
        }
        #endregion

        #region OnStart
        protected override void OnStart(string[] args)
        {
            try
            {
                _log.Info("Starting the Application");
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);


                //Make sure user did not set to zero
                if (intCycleTimeMin <= 0 )
                {
                    _log.Info("CycleTimeMinutes was less than or equal to zero");
                    intCycleTimeMin = 1;
                    _log.Info("Reset to 1");
                }

                _log.Debug($"Cycle time is set to {intCycleTimeMin} min");
                timer.Interval = intCycleTimeMin * 60000; //number in milliseconds  

                if (DateTime.Now.Hour == 0 && DateTime.Now.Minute < 15)
                {
                    _log.Info("Running nightly purge between 00:00 to 00:15");
                    PurgeFiles();
                }

                _log.Debug("Enabling the CycleTimeMinutes Timer");
                timer.Enabled = true;

                CheckDirectories();
                RemoveOldFolders();
                ProcessMessageSystems();
                MoveFiles();
            }
            catch (Exception ex)
            {

                _log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
                throw new Exception(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());

            }
        }
        #endregion 

        #region OnElapsedTime
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            _log.Info("Service is recall");
            _log.Info("Reloading the parameters from the config file incase there were changes");
            Properties.Settings.Default.Reload();

            if (DateTime.Now.Hour == 0 && DateTime.Now.Minute < 15)
            {
                _log.Info("Running nightly purge between 00:00 to 00:15");
                PurgeFiles();
            }

            CheckDirectories();
            RemoveOldFolders();
            ProcessMessageSystems();
            MoveFiles();
        }
        #endregion

        #region OnStop
        protected override void OnStop()
        {
            _log.Info("Service is stopped");
        }
        #endregion

        #region CheckDirectories
        public void CheckDirectories()
        {
            _log.Debug("Checking if the directories exist");
            strWorkFolder = strStorageFolder + @"\EDI_KMC_Service";
            _log.Debug($"Directory {strWorkFolder} exists");

            strLogFolder = strWorkFolder + @"\Log";
            _log.Debug($"Directory {strLogFolder} exists");
                       
            _log.Debug($"Making sure folders the {strLogFolder} exist");
            if (!Directory.Exists(strLogFolder))
            {
                Directory.CreateDirectory(strLogFolder);
                _log.Debug("Created " + strLogFolder + " folder");
            }
            _log.Debug("Finished checking if directories exist");
        }
        #endregion

        #region PurgeFiles
        public void PurgeFiles()
        {
            _log.Info("Purging files");
            strWorkFolder = strStorageFolder + @"\CSCFileService";
            strLogFolder = strWorkFolder + @"\Log";
            string[] logFiles = Directory.GetFiles(strLogFolder);
            foreach (string logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);

                if (fileInfo.CreationTime < DateTime.Now.AddDays(intPurge * -1))
                {
                    _log.Debug("Found Log file(s) that are more then " + intPurge + " days old to delete " + fileInfo.ToString());

                    _log.Debug("Verifying that the old file ends with .log");
                    if (fileInfo.Name.Contains(".log"))
                    {
                        fileInfo.Delete();
                        _log.Debug("Deleted the file " + fileInfo.Name.ToString());
                    }
                    else
                    {
                        _log.Debug("Did not delete the file " + fileInfo.Name.ToString());
                    }
                }
                else
                {
                    _log.Debug("This file" + fileInfo.Name.ToString() + " is not older then " + intPurge + " days");
                }

            }
                        
        }
        #endregion

        #region ProcessMessageSystems
        public void ProcessMessageSystems()
        {
            try
            {
                _log.Info("Starting to look at Message Systems to add new parameters");
                
                //Adding to the database 
                _log.Debug("Loading the tables from the entity framework");
                using (var context = new ESPEntities())
                {
                    //Looking at Message System that are set to Folder type
                    var Lookup = context.mscMessageSystems.Where(m => m.adapter == "Folder")
                                                                    .ToList();
                    _log.Debug("Loaded the message systems that are set as folder type");

                    //Looking through what we found
                    _log.Debug("Going to loop throught the message systems for the ones that does not have a EDI_KMC_Service folder");
                    foreach (var m in Lookup)
                    {

                        _log.Debug($"Looking at the {m.name} message system");
                        var sections = context.infParameters.Where(f => f.sectionID == m.sectionID).ToList();

                        int count = sections.Count;
                        _log.Debug($"Looking through all the {count} paramater sections");
                        foreach (var s in sections)
                        {

                            if (s.name != "EDI_KMC_Service Folder")
                            {
                                _log.Debug($"{s.name} does not match EDI_KMC_Service Folder");
                                count--;
                            }
                            else
                            {
                                _log.Debug($"Found the parameter EDI_KMC_Service no need to add it");
                                count++;
                            }

                        }

                        if (count > 0)
                        {
                            _log.Debug($"Did not find the EDI_KMC_Service folder");
                        }
                        
                        //Create the new parameter folder
                        if (count <= 0)
                        {
                            _log.Debug($"Adding the EDI_KMC_Service folder parameter");

                            var a = new infParameter
                            {

                                mainttime = DateAndTime.Now,
                                userID = "EDI_KMC_Service",
                                name = "EDI_KMC_Service Folder",
                                scope = 0,
                                value = "",
                                comments = "EDI_KMC_Service tool to process files in FIFO order as KMC cannot yet",
                                sectionID = (int)m.sectionID,
                                helpcontextid = null,
                                isaudited = -1,
                                datatype = 0,
                                allowpersonalvalues = null,
                                allowgroupvalues = null,
                                allowplantvalues = null,
                                parametertype = 0,
                                obsolete = null,
                                enumeration = null,
                                businessClassID = null,
                                configurationchangereference = null,
                                configurationnotes = "EDI_KMC_Service tool to process files in FIFO order as KMC cannot yet"

                            };
                            context.infParameters.Add(a);
                            context.SaveChanges();
                            _log.Info($"Added a new folder for the message system called {m.name}");
                        }


                    }
                    
                }

            }
            catch (Exception ex)
            {

                _log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
                throw new Exception(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());

            }
        }
        #endregion

        #region RemoveOldFolders
        public void RemoveOldFolders()
        {
            _log.Info("Starting to look at Message Systems to remove old parameters");

            //Adding to the database 
            _log.Debug("Loading the tables from the entity framework");
            using (var context = new ESPEntities())
            {
                //Looking at Message System that are set to Folder type
                var Lookup = context.mscMessageSystems.Where(m => m.adapter == "Folder")
                                                                .ToList();
                _log.Debug("Loaded the message systems that are set as folder type");

                //Looking through what we found
                _log.Debug("Going to loop throught the message systems paramters for the ones that have a EDIService Folder");
                foreach (var m in Lookup)
                {

                    _log.Debug($"Looking at the {m.name} message system");
                    var oldparam = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                                f.name.Contains("EDIService")).ToList();

                    //_log.Info($"Updating the new parameter EDI_KMC_Service Folder with the old parameter value {p.value}");
                    var newparam = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                    f.name.Contains("EDI_KMC_Service")).ToList();

                    //Copy from Old Parameter to the new one
                    if (oldparam.Count > 0)
                    {
                        if (oldparam[0].value.Length > 0)
                        {
                            _log.Debug($"The new parameter was added with the new name EDI_KMC_Service Folder");
                            foreach (var p in newparam)
                            {
                                _log.Info($"Updating the new parameter value with {p.value}");
                                newparam[0].value = oldparam[0].value;
                                context.infParameters.Attach(newparam[0]);
                                context.Entry(newparam[0]).State = System.Data.Entity.EntityState.Modified;
                                //context.Entry(newparam).Property(newparam[0].value).IsModified = true;
                                context.SaveChanges();
                                _log.Info($"Update of new parameter value to {p.value} completed");

                                _log.Info($"Deleting the old parameter {oldparam[0].name} with the value {oldparam[0].value}");
                                context.infParameters.Attach(oldparam[0]);
                                context.infParameters.Remove(oldparam[0]);
                                context.SaveChanges();
                                _log.Info($"Delete of old parameter {oldparam[0].name} completed");
                            }
                        }
                    }
                    //Rereading the parameters
                    oldparam = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                                f.name.Contains("EDIService")).ToList();

                    newparam = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                    f.name.Contains("EDI_KMC_Service")).ToList();

                    if (oldparam.Count > 0 && newparam.Count > 0)
                    {
                        if (oldparam[0].value.Length == 0 && newparam[0].value.Length == 0)
                        {
                            _log.Debug($"There are parameters with the old name EDIService Folder");
                            foreach (var p in oldparam)
                            {
                                _log.Info($"Deleting the old parameter {p.name} with the value {p.value}");
                                context.infParameters.Attach(p);
                                context.infParameters.Remove(p);
                                context.SaveChanges();
                                _log.Info($"Delete of old parameter {p.name} completed");
                            }
                        }
                    }
                }

             }

        }
        #endregion

        #region MoveFiles
        public void MoveFiles()
        {
            Int32 intInputFileCount = 0;
            string strInputFolder = "";
            string strEDIServiceFolder = "";

            try
            {

                using (var context = new ESPEntities())
                {

                    //Looking at Message System that are set to Folder type
                    var Lookup = context.mscMessageSystems.Where(m => m.status == 0 &&
                                                                    m.adapter == "Folder")
                                                                    .ToList();
                    _log.Debug("Loaded the message systems that are set as folder type");

                    //Looking through what we found
                    _log.Debug("Going to loop throught the message systems for the ones that have EDI_KMC_Service folder setup");
                    foreach (var m in Lookup)
                    {
                        
                        _log.Debug($"Looking at the {m.name} message system");
                        
                        var chkInputFolder = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                                    f.name.Contains("Input Folder Name") 
                                                                    ).ToList();

                        var chkEDIServiceFolder = context.infParameters.Where(f => f.sectionID == m.sectionID &&
                                                                    f.name.Contains("EDI_KMC_Service Folder")
                                                                    ).ToList();

                        _log.Debug($"The message system {m.name} and the folder {chkEDIServiceFolder[0].name} is set for FIFO");
                        
                        //Checking in the input folder exists
                        if(chkInputFolder[0].value == null || chkInputFolder[0].value == "")
                        {
                            _log.Debug($"The input folder is null for message system named {m.name} skiping may need to set it up");
                            continue;
                        }else
                        {
                            _log.Info($"The Input folder does have a value set as {chkInputFolder[0].name} continuing to verify the Input folder");
                            //Checking if input folder exists
                            strInputFolder = chkInputFolder[0].value;
                            if (!Directory.Exists(strInputFolder))
                            {
                                _log.Debug("Creating directory " + strInputFolder);
                                try
                                {
                                    Directory.CreateDirectory(strInputFolder);
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
                                }
                                
                                _log.Debug("Created directory " + strInputFolder);
                            }
                            else
                            {
                                _log.Debug("Folder already exists " + strInputFolder);

                                _log.Debug("Looking in folder: " + strInputFolder);

                                _log.Debug("Sorting the files in write time ascending order");

                                //https://stackoverflow.com/questions/1179970/how-to-find-the-most-recent-file-in-a-directory-using-net-and-without-looping

                                string pattern = "*.*";
                                var dirInfo = new DirectoryInfo(strInputFolder);
                                var files = (from f in dirInfo.GetFiles(pattern) orderby f.LastWriteTime ascending select f).ToList();
                                
                                _log.Debug("Done sorting the files in write time ascending order");

                                if (files.Count > 0)
                                {
                                    intInputFileCount = files.Count;
                                    _log.Info($"Found {files.Count} files..., so we are exiting for this message system until files are processed by KMC");
                                }
                                else
                                {
                                    intInputFileCount = 0;
                                    _log.Info("Did not find any files..., lets go look to see if there are any files waiting to be moved");
                                }

                            }

                        }

                        //Checking is EDI_KMC_Service folder exists
                        if (chkEDIServiceFolder[0].value == null || chkEDIServiceFolder[0].value == "")
                        {
                            _log.Debug($"The input folder is null for message system named {m.name} skiping may need to set it up");
                            continue;
                        }else
                        {
                            _log.Info($"The EDI_KMC_Service folder does have a value set as {chkEDIServiceFolder[0].name} continuing to verify the EDI_KMC_Service folder");
                            //Checking if EDI_KMC_Service exists
                            strEDIServiceFolder = chkEDIServiceFolder[0].value;
                            if (!Directory.Exists(strEDIServiceFolder))
                            {
                                _log.Debug($"Creating directory " + strEDIServiceFolder);
                                try
                                {
                                    Directory.CreateDirectory(strEDIServiceFolder);
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
                                }
                                
                                _log.Debug($"Created directory " + strEDIServiceFolder);
                            }
                            else
                            {
                                _log.Debug($"Folder already exists " + strEDIServiceFolder);

                                if (intInputFileCount == 0)
                                {
                                    _log.Debug($"Looking in folder: " + strEDIServiceFolder);

                                    _log.Debug($"Sorting the files in write time ascending order");

                                    string pattern = "*.*";
                                    var dirInfo = new DirectoryInfo(strEDIServiceFolder);
                                    var files = (from f in dirInfo.GetFiles(pattern) orderby f.LastWriteTime ascending select f).ToList();

                                    if (files.Count > 0)
                                    {
                                        _log.Info($"Found {files.Count} files moving them for processing");
                                        File.Copy(Path.Combine(strEDIServiceFolder, files[0].Name), Path.Combine(strInputFolder, files[0].Name));
                                        _log.Info($"Copied file {Path.Combine(strEDIServiceFolder, files[0].Name)} to {Path.Combine(strInputFolder, files[0].Name)}");
                                        File.Delete(Path.Combine(strEDIServiceFolder, files[0].Name));
                                        _log.Info($"Deleted file {Path.Combine(strEDIServiceFolder, files[0].Name)}");
                                    }
                                    else
                                    {
                                        _log.Info("Did not find any files to process...");
                                    }
                                }
                            
                            }

                        }

                        intInputFileCount = 0;
                        _log.Info($"Resetting the file count in that folder of {intInputFileCount} to 0");
                    }

                }

            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
                throw new Exception(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
            }

        }
        #endregion 

    }
}
