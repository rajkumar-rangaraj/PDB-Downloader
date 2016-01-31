using SymbolFetch.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SymbolFetch
{
    class UrlBuilder
    {
        public string BuildUrl(string filename)
        {
            string downloadURL = string.Empty;
            string SymbolServerUrl;

            if (File.Exists(filename))
            {
                PeHeaderReader reader = new PeHeaderReader(filename);
                //UserAgent:  Microsoft-Symbol-Server/10.0.10036.206
                //Host:  msdl.microsoft.com
                //URI: /download/symbols/iiscore.pdb/6E3058DA562C4EB187071DC08CF7B59E1/iiscore.pdb
                string pdbName;

                if (string.IsNullOrEmpty(reader.pdbName))
                {
                    downloadURL = string.Empty;
                }
                else
                {
                    if (reader.pdbName.Contains("\\"))
                    {
                        pdbName = (reader.pdbName.Split(new char[] { '\\' }))[reader.pdbName.Split(new char[] { '\\' }).Length - 1];
                    }
                    else
                        pdbName = reader.pdbName;

                    SymbolServerUrl = ConfigurationReader.SymbolServerUrl;

                    if (string.IsNullOrEmpty(SymbolServerUrl))
                        downloadURL = "http://msdl.microsoft.com/download/symbols/" + pdbName + "/" + reader.debugGUID.ToString("N").ToUpper() + reader.pdbage + "/" + pdbName;
                    else
                        downloadURL = SymbolServerUrl + "/" + pdbName + "/" + reader.debugGUID.ToString("N").ToUpper() + reader.pdbage + "/" + pdbName;
                }
            }
            return downloadURL;
        }
    }
}
