using SymbolFetch.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SymbolFetch
{
    #region FileDownloader
    class ResourceDownloader : System.Object, IDisposable
    {

        #region Nested Types
        public struct FileInfo
        {
            public string Path;
            public string Name;
            public string PdbGuid;
            public bool IsCompressed;

            public FileInfo(String path)
            {
                this.Path = path;
                this.Name = this.Path.Split("/"[0])[this.Path.Split("/"[0]).Length - 1];
                this.PdbGuid = this.Path.Split("/"[0])[this.Path.Split("/"[0]).Length - 2];
                this.IsCompressed = false;
            }
            
            public void SetPath(string path)
            {
                this.Path = path;
            }

            public void SetName()
            {
                this.Name = this.Path.Split("/"[0])[this.Path.Split("/"[0]).Length - 1];
            }
            public void SetNameUsingPath(string path)
            {
                this.Name = path;
            }

        }


        private enum Event
        {
            CalculationFileSizesStarted,

            FileSizesCalculationComplete,
            DeletingFilesAfterCancel,

            FileDownloadAttempting,
            FileDownloadStarted,
            FileDownloadStopped,
            FileDownloadSucceeded,

            ProgressChanged
        };

        private enum InvokeType
        {
            EventRaiser,
            FileDownloadFailedRaiser,
            CalculatingFileNrRaiser
        };
        #endregion

        #region Events
        public event EventHandler Started;
        public event EventHandler Paused;
        public event EventHandler Resumed;
        public event EventHandler CancelRequested;
        public event EventHandler DeletingFilesAfterCancel;
        public event EventHandler Canceled;
        public event EventHandler Completed;
        public event EventHandler Stopped;

        public event EventHandler IsBusyChanged;
        public event EventHandler IsPausedChanged;
        public event EventHandler StateChanged;

        public event EventHandler CalculationFileSizesStarted;
        public event CalculatingFileSizeEventHandler CalculatingFileSize;
        public event EventHandler FileSizesCalculationComplete;

        public event EventHandler FileDownloadAttempting;
        public event EventHandler FileDownloadStarted;
        public event EventHandler FileDownloadStopped;
        public event EventHandler FileDownloadSucceeded;
        public event FailEventHandler FileDownloadFailed;

        public event EventHandler ProgressChanged;
        #endregion

        #region Fields
        private const Int32 default_decimals = 2;

        // Delegates
        public delegate void FailEventHandler(object sender, Exception ex);
        public delegate void CalculatingFileSizeEventHandler(object sender, Int32 fileNr);

        // The download worker
        private BackgroundWorker bgwDownloader = new BackgroundWorker();

        // Preferences
        private Boolean m_supportsProgress, m_deleteCompletedFiles;
        private Int32 m_packageSize, m_stopWatchCycles;
        public string DownloadLocation;

        // State
        private Boolean m_disposed = false;
        private Boolean m_busy, m_paused, m_canceled;
        private Int64 m_currentFileProgress, m_totalProgress, m_currentFileSize;
        private Int32 m_currentSpeed, m_fileNr;

        // Data
        private String m_localDirectory;
        private List<FileInfo> m_files = new List<FileInfo>();
        private Int64 m_totalSize;

        #endregion

        #region Constructors
        public ResourceDownloader()
        {
            this.initizalize(false);
        }

        public ResourceDownloader(Boolean supportsProgress)
        {
            this.initizalize(supportsProgress);
        }

        private void initizalize(Boolean supportsProgress)
        {
            // Set the bgw properties
            bgwDownloader.WorkerReportsProgress = true;
            bgwDownloader.WorkerSupportsCancellation = true;
            bgwDownloader.DoWork += new DoWorkEventHandler(bgwDownloader_DoWork);
            bgwDownloader.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgwDownloader_RunWorkerCompleted);
            bgwDownloader.ProgressChanged += new ProgressChangedEventHandler(bgwDownloader_ProgressChanged);

            // Set the default class preferences
            this.SupportsProgress = supportsProgress;
            this.PackageSize = 4096;
            this.StopWatchCyclesAmount = 5;
            this.DeleteCompletedFilesAfterCancel = true;
            this.DownloadLocation = !string.IsNullOrEmpty(Constants.DownloadFolder)? Constants.DownloadFolder: "C:\\symcache";
        }
        #endregion

        #region Public methods
        public void Start() { this.IsBusy = true; }

        public void Pause() { this.IsPaused = true; }

        public void Resume() { this.IsPaused = false; }

        public void Stop() { this.IsBusy = false; }
        public void Stop(Boolean deleteCompletedFiles)
        {
            this.DeleteCompletedFilesAfterCancel = deleteCompletedFiles;
            this.Stop();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void SetDownloadLocation(string path)
        {
            DownloadLocation = path;
        }

        #region Size formatting functions
        public static string FormatSizeBinary(Int64 size)
        {
            return ResourceDownloader.FormatSizeBinary(size, default_decimals);
        }
        
        public static string FormatSizeBinary(Int64 size, Int32 decimals)
        {
            String[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            Double formattedSize = size;
            Int32 sizeIndex = 0;
            while (formattedSize >= 1024 && sizeIndex < sizes.Length)
            {
                formattedSize /= 1024;
                sizeIndex += 1;
            }
            return Math.Round(formattedSize, decimals) + sizes[sizeIndex];
        }

        public static string FormatSizeDecimal(Int64 size)
        {
            return ResourceDownloader.FormatSizeDecimal(size, default_decimals);
        }

        public static string FormatSizeDecimal(Int64 size, Int32 decimals)
        {
            String[] sizes = { "B", "kB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            Double formattedSize = size;
            Int32 sizeIndex = 0;
            while (formattedSize >= 1000 && sizeIndex < sizes.Length)
            {
                formattedSize /= 1000;
                sizeIndex += 1;
            }
            return Math.Round(formattedSize, decimals) + sizes[sizeIndex];
        }
        #endregion

        #endregion

        #region Protected methods
        protected virtual void Dispose(Boolean disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    bgwDownloader.Dispose();
                }
                this.Files = null;
            }
        }
        #endregion

        #region Private methods
        private void bgwDownloader_DoWork(object sender, DoWorkEventArgs e)
        {
            Int32 fileNr = 0;

            if (this.SupportsProgress) { calculateFilesSize(); }

            if (!Directory.Exists(this.LocalDirectory)) { Directory.CreateDirectory(this.LocalDirectory); }

            while (fileNr < this.Files.Count && !bgwDownloader.CancellationPending)
            {
                m_fileNr = fileNr;
                downloadFile(fileNr);

                if (bgwDownloader.CancellationPending)
                {
                    fireEventFromBgw(Event.DeletingFilesAfterCancel);
                    cleanUpFiles(this.DeleteCompletedFilesAfterCancel ? 0 : m_fileNr, this.DeleteCompletedFilesAfterCancel ? m_fileNr + 1 : 1);
                }
                else
                {
                    fileNr += 1;
                }
            }
        }

        private void calculateFilesSize()
        {
            fireEventFromBgw(Event.CalculationFileSizesStarted);
            bool headVerb = true;
            m_totalSize = 0;
            string message;
            for (Int32 fileNr = 0; fileNr < this.Files.Count; fileNr++)
            {
                bgwDownloader.ReportProgress((Int32)InvokeType.CalculatingFileNrRaiser, fileNr + 1);
                try
                {
                    //Probe 1
                    HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(this.Files[fileNr].Path);
                    webReq.UserAgent = Constants.SymbolServer;
                    webReq.Method = "HEAD";
                    HttpWebResponse webResp = (HttpWebResponse)webReq.GetResponseNoException();

                    //Probe 2
                    if (webResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        webResp = Retry(fileNr, headVerb);
                    }

                    if (webResp.StatusCode == HttpStatusCode.OK)
                    {
                        m_totalSize += webResp.ContentLength;
                    }

                    //Probe 3
                    if (webResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        webResp = RetryFilePointer(fileNr);

                        if (webResp.StatusCode == HttpStatusCode.OK)
                        {
                            string ignore = null;
                            m_totalSize += ProcessFileSize(webResp, out ignore);                     
                        }
                    }


                    webResp.Close();
                }
                catch (Exception ex)
                {
                    message = ex.Message.ToString();
                }
            }
            fireEventFromBgw(Event.FileSizesCalculationComplete);
        }

        private HttpWebResponse Retry(int fileNr, bool headVerb)
        {
            string path = this.Files[fileNr].Path;
            path = ProbeWithUnderscore(path);
            var webReq = (HttpWebRequest)System.Net.WebRequest.Create(path);
            webReq.UserAgent = Constants.SymbolServer;
            if(headVerb)
                webReq.Method = "HEAD";
            return (HttpWebResponse)webReq.GetResponseNoException();
        }

        private HttpWebResponse RetryFilePointer(int fileNr)
        {
            string path = this.Files[fileNr].Path;
            path = ProbeWithFilePointer(path);
            var webReq = (HttpWebRequest)System.Net.WebRequest.Create(path);
            webReq.UserAgent = Constants.SymbolServer;
            return (HttpWebResponse)webReq.GetResponseNoException();
        } 

        private long ProcessFileSize(HttpWebResponse webResp, out string filePath)
        {
            long length = 0;
            filePath = null;
            Stream receiveStream = webResp.GetResponseStream();
            Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
            StreamReader readStream = new StreamReader(receiveStream, encode);
            Char[] read = new Char[webResp.ContentLength];
            readStream.Read(read, 0, (int)webResp.ContentLength);

            string file = new string(read, 0, (int)webResp.ContentLength);

            if (file.Contains("PATH"))
            {
                file = file.Substring(5, file.Length - 5); //Removing PATH: from the output

                try
                {
                    System.IO.FileInfo fInfo = new System.IO.FileInfo(file);
                    if (fInfo.Exists)   
                    {
                        length = fInfo.Length;
                        filePath = file;
                    }
                }
                catch(Exception ex)
                {
                    WriteToLog(file, ex);
                }
            }
            else
            {
                int position= webResp.ResponseUri.PathAndQuery.IndexOf(".pdb");
                string fileName = webResp.ResponseUri.PathAndQuery.Substring(1, position + 3);
                if (!FailedFiles.ContainsKey(fileName))
                    FailedFiles.Add(fileName, " - No matching PDBs found - " + file);
            }

            return length;
        }

        private void DownloadFile(string srcFile, string filePath)
        {
            File.Copy(srcFile, filePath, true);
        }

        private static string ProbeWithUnderscore(string path)
        {
            path = path.Remove(path.Length - 1);
            path = path.Insert(path.Length, "_");
            return path;
        }

        private static string ProbeWithFilePointer(string path)
        {
            int position  = path.LastIndexOf('/');
            path = path.Remove(position, (path.Length - position));
            path = path.Insert(path.Length, "/file.ptr");
            return path;
        }

        private void fireEventFromBgw(Event eventName)
        {
            bgwDownloader.ReportProgress((int)InvokeType.EventRaiser, eventName);
        }

        private void downloadFile(Int32 fileNr)
        {
            bool headVerb = false;
            m_currentFileSize = 0;
            bool fileptr = false;
            fireEventFromBgw(Event.FileDownloadAttempting);

            FileInfo file = this.Files[fileNr];
            
            Int64 size = 0;

            Byte[] readBytes = new Byte[this.PackageSize];
            Int32 currentPackageSize;
            System.Diagnostics.Stopwatch speedTimer = new System.Diagnostics.Stopwatch();
            Int32 readings = 0;
            Exception exc = null;

            FileStream writer;
            string dirPath = DownloadLocation + "\\" + file.Name + "\\" + file.PdbGuid;
            string downloadUrl = this.Files[fileNr].Path;

            HttpWebRequest webReq;
            HttpWebResponse webResp = null;

            try
            {
                webReq = (HttpWebRequest)System.Net.WebRequest.Create(downloadUrl);
                webReq.UserAgent = Constants.SymbolServer;
                webResp = (HttpWebResponse)webReq.GetResponseNoException();
                if (webResp.StatusCode == HttpStatusCode.NotFound)
                {
                    webResp = Retry(fileNr, headVerb);

                    if (webResp.StatusCode == HttpStatusCode.OK)
                    {
                        file.IsCompressed = true;
                        size = webResp.ContentLength;
                    }

                    if (webResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        webResp = RetryFilePointer(fileNr);
                        fileptr = true;
                    }

                    if (webResp.StatusCode != HttpStatusCode.OK)
                    {
                        if (!FailedFiles.ContainsKey(file.Name))
                            FailedFiles.Add(file.Name, " - " + webResp.StatusCode + "  " + webResp.StatusDescription);
                    }
                }
                else if(webResp.StatusCode == HttpStatusCode.OK)
                    size = webResp.ContentLength;

            }
            catch (Exception ex)
            {
                exc = ex;
                WriteToLog(file.Name, exc);
            }
            if (webResp.StatusCode == HttpStatusCode.OK)
            {
                Directory.CreateDirectory(dirPath);
                
                if (fileptr)
                {
                    string filePath = dirPath + "\\" +
                        file.Name;
                    string srcFile = null;
                    FileStream reader;
                    size = ProcessFileSize(webResp, out srcFile);
                    m_currentFileSize = size;

                    if (srcFile != null)
                    {
                        reader = new FileStream(srcFile, FileMode.Open, FileAccess.Read);
                        writer = new FileStream(filePath,
                            System.IO.FileMode.Create);

                        //   DownloadFile(srcFile, filePath);
                        fireEventFromBgw(Event.FileDownloadStarted);
                        m_currentFileProgress = 0;
                        while (m_currentFileProgress < size && !bgwDownloader.CancellationPending)
                        {
                            while (this.IsPaused) { System.Threading.Thread.Sleep(100); }

                            speedTimer.Start();

                            currentPackageSize = reader.Read(readBytes, 0, this.PackageSize);

                            m_currentFileProgress += currentPackageSize;
                            m_totalProgress += currentPackageSize;
                            fireEventFromBgw(Event.ProgressChanged);

                            writer.Write(readBytes, 0, currentPackageSize);
                            readings += 1;

                            if (readings >= this.StopWatchCyclesAmount)
                            {
                                m_currentSpeed = (Int32)(this.PackageSize * StopWatchCyclesAmount * 1000 / (speedTimer.ElapsedMilliseconds + 1));
                                speedTimer.Reset();
                                readings = 0;
                            }
                        }
                        reader.Close();
                        writer.Close();
                        speedTimer.Stop();
                        //end
                    }
                }
                else
                {
                    m_currentFileSize = size;
                    //string name;
                    if (file.IsCompressed)
                    {
                        file.Name = ProbeWithUnderscore(file.Name);
                    }
                    string filePath = dirPath + "\\" +
                        file.Name;
                    writer = new FileStream(filePath,
                        System.IO.FileMode.Create);

                    if (exc != null)
                    {
                        bgwDownloader.ReportProgress((Int32)InvokeType.FileDownloadFailedRaiser, exc);
                    }
                    else
                    {
                        m_currentFileProgress = 0;
                        while (m_currentFileProgress < size && !bgwDownloader.CancellationPending)
                        {
                            while (this.IsPaused) { System.Threading.Thread.Sleep(100); }

                            speedTimer.Start();

                            currentPackageSize = webResp.GetResponseStream().Read(readBytes, 0, this.PackageSize);

                            m_currentFileProgress += currentPackageSize;
                            m_totalProgress += currentPackageSize;
                            fireEventFromBgw(Event.ProgressChanged);

                            writer.Write(readBytes, 0, currentPackageSize);
                            readings += 1;

                            if (readings >= this.StopWatchCyclesAmount)
                            {
                                m_currentSpeed = (Int32)(this.PackageSize * StopWatchCyclesAmount * 1000 / (speedTimer.ElapsedMilliseconds + 1));
                                speedTimer.Reset();
                                readings = 0;
                            }
                        }

                        speedTimer.Stop();
                        writer.Close();

                        webResp.Close();
                        if (file.IsCompressed)
                        {
                            HandleCompression(filePath);
                        }
                       
                    }
                    if (!bgwDownloader.CancellationPending) { fireEventFromBgw(Event.FileDownloadSucceeded); }
                }               
            }
            fireEventFromBgw(Event.FileDownloadStopped);
        }

        public static void WriteToLog(string fileName, Exception exc)
        {
            using (FileStream fs = new FileStream("Log.txt", FileMode.Append))
            using (StreamWriter sr = new StreamWriter(fs))
            {
                sr.WriteLine(DateTime.Now.ToString() + "   " + fileName + " - " + exc.Message);
            }
        }

        public static void WriteToLog(string fileName, string text)
        {
            using (FileStream fs = new FileStream("Log.txt", FileMode.Append))
            using (StreamWriter sr = new StreamWriter(fs))
            {
                sr.WriteLine(DateTime.Now.ToString() + "   " + fileName + " - " + text);
            }
        }


        private void HandleCompression(string filePath)
        {
            string uncompressedFilePath = filePath.Remove(filePath.Length - 1);
            uncompressedFilePath = uncompressedFilePath.Insert(uncompressedFilePath.Length, "b");
            string args = string.Format("expand {0} {1}", "\"" + filePath + "\"", "\"" + uncompressedFilePath + "\"");

            Match m = Regex.Match(args, "^\\s*\"(.*?)\"\\s*(.*)");
            if (!m.Success)
                m = Regex.Match(args, @"\s*(\S*)\s*(.*)");    // thing before first space is command

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo(m.Groups[1].Value, m.Groups[2].Value);

            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            
            startInfo.UseShellExecute = false;
            startInfo.Verb = "runas";
            startInfo.CreateNoWindow = true;
            process.StartInfo = startInfo;
            
            try
            {
                var started = process.Start();
                if (started)
                {
                    process.WaitForExit(600000);
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                WriteToLog(filePath, ex);
            }
        }

        private void cleanUpFiles(Int32 start, Int32 length)
        {
            Int32 last = length < 0 ? this.Files.Count - 1 : start + length - 1;

            for (Int32 fileNr = start; fileNr <= last; fileNr++)
            {
                String fullPath = this.LocalDirectory + "\\" + this.Files[fileNr].Name;
                if (System.IO.File.Exists(fullPath)) { System.IO.File.Delete(fullPath); }
            }
        }

        private void bgwDownloader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            m_paused = false;
            m_busy = false;

            if (this.HasBeenCanceled)
            {
                if (Canceled != null) { this.Canceled(this, new EventArgs()); }
            }
            else
            {
                if (Completed != null) { this.Completed(this, new EventArgs()); }
            }

            if (Stopped != null) { this.Stopped(this, new EventArgs()); }
            if (IsBusyChanged != null) { this.IsBusyChanged(this, new EventArgs()); }
            if (StateChanged != null) { this.StateChanged(this, new EventArgs()); }
        }

        private void bgwDownloader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch ((InvokeType)e.ProgressPercentage)
            {
                case InvokeType.EventRaiser:
                    switch ((Event)e.UserState)
                    {
                        case Event.CalculationFileSizesStarted:
                            if (CalculationFileSizesStarted != null) { this.CalculationFileSizesStarted(this, new EventArgs()); }
                            break;
                        case Event.FileSizesCalculationComplete:
                            if (FileSizesCalculationComplete != null) { this.FileSizesCalculationComplete(this, new EventArgs()); }
                            break;
                        case Event.DeletingFilesAfterCancel:
                            if (DeletingFilesAfterCancel != null) { this.DeletingFilesAfterCancel(this, new EventArgs()); }
                            break;

                        case Event.FileDownloadAttempting:
                            if (FileDownloadAttempting != null) { this.FileDownloadAttempting(this, new EventArgs()); }
                            break;
                        case Event.FileDownloadStarted:
                            if (FileDownloadStarted != null) { this.FileDownloadStarted(this, new EventArgs()); }
                            break;
                        case Event.FileDownloadStopped:
                            if (FileDownloadStopped != null) { this.FileDownloadStopped(this, new EventArgs()); }
                            break;
                        case Event.FileDownloadSucceeded:
                            if (FileDownloadSucceeded != null) { this.FileDownloadSucceeded(this, new EventArgs()); }
                            break;
                        case Event.ProgressChanged:
                            if (ProgressChanged != null) { this.ProgressChanged(this, new EventArgs()); }
                            break;
                    }
                    break;
                case InvokeType.FileDownloadFailedRaiser:
                    if (FileDownloadFailed != null) { this.FileDownloadFailed(this, (Exception)e.UserState); }
                    break;
                case InvokeType.CalculatingFileNrRaiser:
                    if (CalculatingFileSize != null) { this.CalculatingFileSize(this, (Int32)e.UserState); }
                    break;
            }
        }
        #endregion

        #region Properties
        public List<FileInfo> Files
        {
            get { return m_files; }
            set
            {
                if (this.IsBusy)
                {
                    throw new InvalidOperationException("You can not change the file list during the download");
                }
                else
                {
                    if (this.Files != null) m_files = value;
                }
            }
        }

        public Dictionary<string,string> FailedFiles = new Dictionary<string, string>();

        public String LocalDirectory
        {
            get { return m_localDirectory; }
            set
            {
                if (this.LocalDirectory != value) { m_localDirectory = value; }
            }
        }

        public Boolean SupportsProgress
        {
            get { return m_supportsProgress; }
            set
            {
                if (this.IsBusy)
                {
                    throw new InvalidOperationException("You can not change the SupportsProgress property during the download");
                }
                else
                {
                    m_supportsProgress = value;
                }
            }
        }

        public Boolean DeleteCompletedFilesAfterCancel
        {
            get { return m_deleteCompletedFiles; }
            set { m_deleteCompletedFiles = value; }
        }

        public Int32 PackageSize
        {
            get { return m_packageSize; }
            set
            {
                if (value > 0)
                {
                    m_packageSize = value;
                }
                else
                {
                    throw new InvalidOperationException("The PackageSize needs to be greather then 0");
                }
            }
        }

        public Int32 StopWatchCyclesAmount
        {
            get { return m_stopWatchCycles; }
            set
            {
                if (value > 0)
                {
                    m_stopWatchCycles = value;
                }
                else
                {
                    throw new InvalidOperationException("The StopWatchCyclesAmount needs to be greather then 0");
                }
            }
        }

        public Boolean IsBusy
        {
            get { return m_busy; }
            set
            {
                if (this.IsBusy != value)
                {
                    m_busy = value;
                    m_canceled = !value;
                    if (this.IsBusy)
                    {
                        m_totalProgress = 0;
                        bgwDownloader.RunWorkerAsync();

                        if (Started != null) { this.Started(this, new EventArgs()); }
                        if (IsBusyChanged != null) { this.IsBusyChanged(this, new EventArgs()); }
                        if (StateChanged != null) { this.StateChanged(this, new EventArgs()); }
                    }
                    else
                    {
                        m_paused = false;
                        bgwDownloader.CancelAsync();
                        if (CancelRequested != null) { this.CancelRequested(this, new EventArgs()); }
                        if (StateChanged != null) { this.StateChanged(this, new EventArgs()); }
                    }
                }
            }
        }

        public Boolean IsPaused
        {
            get { return m_paused; }
            set
            {
                if (this.IsBusy)
                {
                    if (this.IsPaused != value)
                    {
                        m_paused = value;
                        if (this.IsPaused)
                        {
                            if (Paused != null) { this.Paused(this, new EventArgs()); }
                        }
                        else
                        {
                            if (Resumed != null) { this.Resumed(this, new EventArgs()); }
                        }
                        if (IsPausedChanged != null) { this.IsPausedChanged(this, new EventArgs()); }
                        if (StateChanged != null) { this.StateChanged(this, new EventArgs()); }
                    }
                }
                else
                {
                    throw new InvalidOperationException("You can not change the IsPaused property when the FileDownloader is not busy");
                }
            }
        }

        public Boolean CanStart
        {
            get { return !this.IsBusy; }
        }

        public Boolean CanPause
        {
            get { return this.IsBusy && !this.IsPaused && !bgwDownloader.CancellationPending; }
        }

        public Boolean CanResume
        {
            get { return this.IsBusy && this.IsPaused && !bgwDownloader.CancellationPending; }
        }

        public Boolean CanStop
        {
            get { return this.IsBusy && !bgwDownloader.CancellationPending; }
        }

        public Int64 TotalSize
        {
            get
            {
                if (this.SupportsProgress)
                {
                    return m_totalSize;
                }
                else
                {
                    throw new InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress to get the total size.");
                }
            }
        }

        public Int64 TotalProgress
        {
            get { return m_totalProgress; }
        }

        public Int64 CurrentFileProgress
        {
            get { return m_currentFileProgress; }
        }

        public Double TotalPercentage()
        {
            return this.TotalPercentage(default_decimals);
        }

        public Double TotalPercentage(Int32 decimals)
        {
            if (this.SupportsProgress)
            {
                return Math.Round((Double)this.TotalProgress / this.TotalSize * 100, decimals);
            }
            else
            {
                throw new InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress.");
            }
        }

        public Double CurrentFilePercentage()
        {
            return this.CurrentFilePercentage(default_decimals);
        }

        public Double CurrentFilePercentage(Int32 decimals)
        {
            return Math.Round((Double)this.CurrentFileProgress / this.CurrentFileSize * 100, decimals);
        }

        public Int32 DownloadSpeed
        {
            get { return m_currentSpeed; }
        }

        public FileInfo CurrentFile
        {
            get { return this.Files[m_fileNr]; }
        }

        public Int64 CurrentFileSize
        {
            get { return m_currentFileSize; }
        }

        public Boolean HasBeenCanceled
        {
            get { return m_canceled; }
        }
        #endregion

    }
    #endregion

}