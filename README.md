<p>Today, in order to download Microsoft symbol files, you have to collect a memory dump of the process and use debuggers, like Debug Diagnostics or VS Debugger, to download it.  The debuggers will attempt to download all the symbols used by the application. This process is very time consuming because it downloads PDBs for all the files. In addition, some of the PDB files are huge, which consumes disk space.</p>

To address these limitations, we have created the PDB Downloader. The PDB Downloader downloads symbol files without collecting any memory dumps. This tool provides an option to select the libraries for which symbols needs to be downloaded. This reduces both time spent and disk space.

<b>Advantages</b>
<ul>
<li>No debuggers are required to download the symbols.</li>
<li>You do not need admin access.</li>
<li>No need to install the tool -  it’s a standalone executable.</li>
<li>You can download symbols which are required by debugger for breakpoints.</li>
<li>The tool reduces 90% of symbol download time.</li>
<li>Disk space utilization is minimal.</li>
</ul>
<b>Features</b>

<ul>
<li>Lightweight tool  -  less than 200 KB in size.</li>
<li>Open source, free to download and modify</li>
<li>Log file support to troubleshoot issues with tool.</li>
<li>Supports both managed and native libraries/executables </li>
<li>Downloads:</li>
<ul>
<li>Microsoft Symbol Server symbols.</li>
<li>Symbols from most external symbol servers, like Google, Adobe, etc.</li>
<li>Private symbols if the symbols servers are configured for HTTP.</li>
<li>Symbols for 32 bit and 64 bit architecture.</li>
</ul>
</ul>

<b>Reference:</b>
https://blogs.msdn.microsoft.com/webtopics/2016/03/07/pdb-downloader/ <br/>

<b>Usage:</b>
https://blogs.msdn.microsoft.com/chiranth/2015/12/22/slow-response-automated-data-collection-using-freb-and-debug-diag/ <br/>
https://blogs.msdn.microsoft.com/chiranth/2015/12/14/session-loss-due-to-application-domain-recycle-advanced/ <br/>
