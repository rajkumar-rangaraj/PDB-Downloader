using System.Configuration;

namespace SymbolFetch.Helpers
{
    public static class Constants
    {

        #region Settings
        public static string SymbolServer;
        public static string DownloadFolder;
        public static bool EnableBulkDownload;
        #endregion

        #region Ctor
        static Constants()
        {
            SymbolServer = @"Microsoft-Symbol-Server/10.0.10522.521";
            DownloadFolder = @"c:\symbols";
            EnableBulkDownload = false;
        }

        #endregion
    }

    public static class ConfigurationReader
    {
        #region Settings
        public static string SymbolServerUrl;
        #endregion

        #region Ctor
        static ConfigurationReader()
        {
            SymbolServerUrl = ConfigurationManager.AppSettings["SymbolServer"];
        }
        #endregion
    }
}
