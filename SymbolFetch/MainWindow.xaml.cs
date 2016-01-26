using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SymbolFetch
{
    public partial class MainWindow : Window
    {
        #region Private variables

        private ResourceDownloader downloader = new ResourceDownloader();

        #endregion

        #region C'Tor
        public MainWindow()
        {
            InitializeComponent();

            WireupCommandBindings();
            WireupDownloaderEvents();
            SetDownloadLocation();
            btnBug.Content = "Feedback/Bug";
            btnBulk.Visibility = Helpers.Constants.EnableBulkDownload ? Visibility.Visible : Visibility.Hidden;
        }

        #endregion

        #region Private methods

        private void WireupCommandBindings()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
            new ExecutedRoutedEventHandler(delegate (object sender, ExecutedRoutedEventArgs args) { this.Close(); })));
        }

        private void WireupDownloaderEvents()
        {
            downloader.StateChanged += new EventHandler(downloader_StateChanged);
            downloader.CalculatingFileSize += new ResourceDownloader.CalculatingFileSizeEventHandler(downloader_CalculationFileSize);
            downloader.ProgressChanged += new EventHandler(downloader_ProgressChanged);
            downloader.FileDownloadAttempting += new EventHandler(downloader_FileDownloadAttempting);
            downloader.FileDownloadStarted += new EventHandler(downloader_FileDownloadStarted);
            downloader.Completed += new EventHandler(downloader_Completed);
            downloader.CancelRequested += new EventHandler(downloader_CancelRequested);
            downloader.DeletingFilesAfterCancel += new EventHandler(downloader_DeletingFilesAfterCancel);
            downloader.Canceled += new EventHandler(downloader_Canceled);
            downloader.FileDownloadSucceeded += Downloader_FileDownloadSucceeded;
        }

        private void SetDownloadLocation()
        {
            btnPath.Content = "Saving to: " + downloader.DownloadLocation;
        }

        #endregion

        #region Events

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Height -= 30;
        }


        private void Downloader_FileDownloadSucceeded(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }


        private void btnStart_Click(object sender, RoutedEventArgs e)
        {

            var builder = new UrlBuilder();


            downloader.LocalDirectory = downloader.DownloadLocation;

            downloader.Files.Clear();

            foreach (var item in lstFiles.Items)
            {
                var downloadURL = builder.BuildUrl(item.ToString());

                if (!string.IsNullOrEmpty(downloadURL))
                {
                    var fileInfo = new ResourceDownloader.FileInfo(downloadURL);
                    downloader.Files.Add(fileInfo);
                }
                else
                    downloader.FailedFiles.Add(item.ToString(), ": Invalid download URL, not probing");
            }

            downloader.Start();

        }

        private void btnopenFile_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            var listOfFiles = new List<string>();
            fileDialog.Multiselect = true;
            fileDialog.Filter = "DLL|*.dll|Executable Files| *.exe";
            if (fileDialog.ShowDialog() == true)
            {
                foreach (var item in fileDialog.FileNames)
                {
                    listOfFiles.Add(item);
                }
                pBarTotalProgress.Value = 0;
                pBarFileProgress.Value = 0;

                lblStatus.Content = "Status: ";
                lblFileSize.Content = "File Size: ";
                lblFileProgress.Content = "-";
                lblTotalProgress.Content = "-";

                lstFiles.Visibility = Visibility.Visible;
                lstFiles.ItemsSource = listOfFiles;
            }
        }

        private void btnPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                downloader.DownloadLocation = openFolderDialog.SelectedPath;
                SetDownloadLocation();
            }

        }

        private void btnBug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("mailto:rajrang@microsoft.com;puluthra@microsoft.com;nanram@microsoft.com?subject=PDB Downloader ");
            }
            catch (Exception ex)
            {
                //TODO: eat it for now
            }

        }

        private void btnBulk_Click(object sender, RoutedEventArgs e)
        {
            string bulkFilePath = string.Empty;
            string line = string.Empty;
            var fileList = new List<string>();
            var fileDialog = new OpenFileDialog();
            fileDialog.Filter = "txt|*.txt";

            if (fileDialog.ShowDialog() == true)
            {
                bulkFilePath = fileDialog.FileName;
                StreamReader reader = new StreamReader(bulkFilePath);
                while ((line = reader.ReadLine()) != null)
                {
                    fileList.Add(line);
                }
                if (fileList.Count > 0)
                {
                    lstFiles.Visibility = Visibility.Visible;
                    lstFiles.ItemsSource = fileList;
                }
            }

        }
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            downloader.Pause();
        }

        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            downloader.Resume();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            downloader.Stop();
        }

        private void downloader_StateChanged(object sender, EventArgs e)
        {
            btnStart.IsEnabled = downloader.CanStart;
            //btnStop.IsEnabled = downloader.CanStop;
            btnPause.IsEnabled = downloader.CanPause;
            btnResume.IsEnabled = downloader.CanResume;

            lstFiles.IsEnabled = !downloader.IsBusy;
            cbUseProgress.IsEnabled = !downloader.IsBusy;
        }


        private void downloader_CalculationFileSize(object sender, Int32 fileNr)
        {
            lblStatus.Content = String.Format("Initializing - file {0} of {1}", fileNr, downloader.Files.Count);
        }

        private void downloader_ProgressChanged(object sender, EventArgs e)
        {
            try
            {
                pBarFileProgress.Value = downloader.CurrentFilePercentage();
                lblFileProgress.Content = String.Format("Downloaded {0} of {1} ({2}%)", ResourceDownloader.FormatSizeBinary(downloader.CurrentFileProgress), ResourceDownloader.FormatSizeBinary(downloader.CurrentFileSize), downloader.CurrentFilePercentage()) + String.Format(" - {0}/s", ResourceDownloader.FormatSizeBinary(downloader.DownloadSpeed));

                if (downloader.SupportsProgress)
                {
                    pBarTotalProgress.Value = downloader.TotalPercentage();
                    lblTotalProgress.Content = String.Format("Downloaded {0} of {1} ({2}%)", ResourceDownloader.FormatSizeBinary(downloader.TotalProgress), ResourceDownloader.FormatSizeBinary(downloader.TotalSize), downloader.TotalPercentage());
                }
            }
            catch (Exception ex)
            { //eat it, not cool I know
            }
        }

        private void downloader_FileDownloadAttempting(object sender, EventArgs e)
        {
            lblStatus.Content = String.Format("Preparing {0}", downloader.CurrentFile.Path);
        }

        private void downloader_FileDownloadStarted(object sender, EventArgs e)
        {
            lblStatus.Content = String.Format("Downloading {0}", downloader.CurrentFile.Path);
            lblFileSize.Content = String.Format("File size: {0}", ResourceDownloader.FormatSizeBinary(downloader.CurrentFileSize));
            //lblSavingTo.Content = String.Format("Saving to {0}\\{1}", downloader.LocalDirectory, downloader.CurrentFile.Name);
        }

        private void downloader_Completed(object sender, EventArgs e)
        {
            lblStatus.Content = String.Format("Download complete, downloaded {0} file(s).", downloader.Files.Count);
            if (downloader.Files.Count > 0 && pBarTotalProgress.Value > 0)
            {
                pBarTotalProgress.Value = 100;
                pBarFileProgress.Value = 100;
            }
            if (downloader.Files.Count > 0 && downloader.Files.Count != downloader.FailedFiles.Count)
                Process.Start(downloader.DownloadLocation);
            if (downloader.FailedFiles.Count > 0)
            {

                using (FileStream fs = new FileStream("Log.txt", FileMode.Append))
                using (StreamWriter sr = new StreamWriter(fs))
                {
                    foreach (var item in downloader.FailedFiles)
                    {
                        sr.WriteLine(item.Key + (!string.IsNullOrEmpty(item.Value) ? item.Value : ": Failure after probing"));
                    }
                }

                MessageBox.Show("Some symbols could not be downloaded. Please check the log file for more info.", "Error");
                downloader.FailedFiles = new Dictionary<string, string>();
            }
        }

        private void downloader_CancelRequested(object sender, EventArgs e)
        {
            lblStatus.Content = "Canceling downloads...";
        }

        private void downloader_DeletingFilesAfterCancel(object sender, EventArgs e)
        {
            lblStatus.Content = "Canceling downloads - deleting files...";
        }

        private void downloader_Canceled(object sender, EventArgs e)
        {
            lblStatus.Content = "Download(s) canceled";
            pBarFileProgress.Value = 0;
            pBarTotalProgress.Value = 0;
            lblFileProgress.Content = "-";
            lblTotalProgress.Content = "-";
            lblFileSize.Content = "-";
            //lblSavingTo.Content = "-";
        }

        private void cbUseProgress_Checked(object sender, RoutedEventArgs e)
        {
            downloader.SupportsProgress = (Boolean)cbUseProgress.IsChecked;
        }

        private void cbDeleteCompletedFiles_Checked(object sender, RoutedEventArgs e)
        {
            downloader.DeleteCompletedFilesAfterCancel = (Boolean)cbDeleteCompletedFiles.IsChecked;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

    }
}
