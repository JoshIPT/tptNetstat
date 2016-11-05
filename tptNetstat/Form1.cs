using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace tptNetstat
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        List<ServiceInfo> services = ReadServicesFile();

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            runNetstat();
        }

        public void runNetstat()
        {
            listView1.Items.Clear();
            DoubleBuffered = true;
            toolStripStatusLabel1.Text = "Refreshing...";
            Thread runner = new Thread(new ThreadStart(runnerThread));
            runner.Start();

        }

        private void runnerThread()
        {
            listView1.BeginUpdate();
            ManagedIpHelper.UpdateExtendedTcpTable(true, IpHelper.TcpTableType.OwnerPidAll);
            foreach (TcpRow row in ManagedIpHelper.TcpRows)
            {
                string procname;
                try
                {
                    Process process = Process.GetProcessById(row.ProcessId);
                    if (process.ProcessName != "System") procname = Path.GetFileName(process.MainModule.FileName);
                    else procname = "System";
                }
                catch (Exception ex)
                {
                    procname = "Unknown";
                }
                ListViewItem itm = new ListViewItem(procname);
                itm.SubItems.Add(row.State.ToString());
                string serviceString = "";
                if (row.State == System.Net.NetworkInformation.TcpState.Listen)
                {
                    itm.SubItems.Add(row.LocalEndPoint.Port.ToString());
                    var svc = services.FirstOrDefault(s => s.Port == row.LocalEndPoint.Port && s.Type.Equals("tcp"));
                    if (svc != null) serviceString = svc.Name;
                    else { serviceString = "Unknown"; }
                }
                else
                {
                    itm.SubItems.Add(row.RemoteEndPoint.Port.ToString());
                    var svc = services.FirstOrDefault(s => s.Port == row.RemoteEndPoint.Port && s.Type.Equals("tcp"));
                    if (svc != null) serviceString = svc.Name;
                    else { serviceString = "Unknown"; }
                }
                itm.SubItems.Add(serviceString);
                itm.SubItems.Add(row.RemoteEndPoint.Address.ToString() + ":" + row.RemoteEndPoint.Port.ToString());
                itm.SubItems.Add(row.LocalEndPoint.Address.ToString() + ":" + row.LocalEndPoint.Port.ToString());
                listView1.Items.Add(itm);
            }
            toolStripStatusLabel1.Text = "Done";
            listView1.EndUpdate();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        class ServiceInfo
        {
            public ushort Port { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
        }

        static List<ServiceInfo> ReadServicesFile()
        {
            var sysFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!sysFolder.EndsWith("\\"))
                sysFolder += "\\";
            var svcFileName = sysFolder + "drivers\\etc\\services";
            var lines = File.ReadAllLines(svcFileName);
            var result = new List<ServiceInfo>();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;
                var info = new ServiceInfo();
                var index = 0;
                info.Name = line.Substring(index, 16).Trim();
                index += 16;
                var temp = line.Substring(index, 9).Trim();
                var tempSplitted = temp.Split('/');
                info.Port = ushort.Parse(tempSplitted[0]);
                info.Type = tempSplitted[1].ToLower();
                result.Add(info);
            }
            return result;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            runNetstat();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            about aboutFrm = new tptNetstat.about();
            aboutFrm.ShowDialog();
        }
    }

#if !MONO
    #region Managed IP Helper API

    public struct TcpTable : IEnumerable<TcpRow>
    {
        #region Private Fields

        private IEnumerable<TcpRow> tcpRows;

        #endregion

        #region Constructors

        public TcpTable(IEnumerable<TcpRow> tcpRows)
        {
            this.tcpRows = tcpRows;
        }

        #endregion

        #region Public Properties

        public IEnumerable<TcpRow> Rows
        {
            get
            {
                return this.tcpRows;
            }
        }

        #endregion

        #region IEnumerable<TcpRow> Members

        public IEnumerator<TcpRow> GetEnumerator()
        {
            return this.tcpRows.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.tcpRows.GetEnumerator();
        }

        #endregion
    }

    public struct TcpRow
    {
        #region Private Fields

        private IPEndPoint localEndPoint;
        private IPEndPoint remoteEndPoint;
        private TcpState state;
        private int processId;

        #endregion

        #region Constructors

        public TcpRow(IpHelper.TcpRow tcpRow)
        {
            this.state = tcpRow.state;
            this.processId = tcpRow.owningPid;

            int localPort = (tcpRow.localPort1 << 8) + (tcpRow.localPort2) + (tcpRow.localPort3 << 24) + (tcpRow.localPort4 << 16);
            long localAddress = tcpRow.localAddr;
            this.localEndPoint = new IPEndPoint(localAddress, localPort);

            int remotePort = (tcpRow.remotePort1 << 8) + (tcpRow.remotePort2) + (tcpRow.remotePort3 << 24) + (tcpRow.remotePort4 << 16);
            long remoteAddress = tcpRow.remoteAddr;
            this.remoteEndPoint = new IPEndPoint(remoteAddress, remotePort);
        }

        #endregion

        #region Public Properties

        public IPEndPoint LocalEndPoint
        {
            get
            {
                return this.localEndPoint;
            }
        }

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return this.remoteEndPoint;
            }
        }

        public TcpState State
        {
            get
            {
                return this.state;
            }
        }

        public int ProcessId
        {
            get
            {
                return this.processId;
            }
        }

        #endregion
    }

    public static class ManagedIpHelper
    {
        public static readonly List<TcpRow> TcpRows = new List<TcpRow>();

        #region Public Methods

        public static void UpdateExtendedTcpTable(bool sorted, IpHelper.TcpTableType tabletype)
        {
            TcpRows.Clear();

            IntPtr tcpTable = IntPtr.Zero;
            int tcpTableLength = 0;

            if (IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, sorted, IpHelper.AfInet, tabletype, 0) != 0)
            {
                try
                {
                    tcpTable = Marshal.AllocHGlobal(tcpTableLength);
                    if (IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, true, IpHelper.AfInet, tabletype, 0) == 0)
                    {
                        IpHelper.TcpTable table = (IpHelper.TcpTable)Marshal.PtrToStructure(tcpTable, typeof(IpHelper.TcpTable));

                        IntPtr rowPtr = (IntPtr)((long)tcpTable + Marshal.SizeOf(table.Length));
                        for (int i = 0; i < table.Length; ++i)
                        {
                            TcpRows.Add(new TcpRow((IpHelper.TcpRow)Marshal.PtrToStructure(rowPtr, typeof(IpHelper.TcpRow))));
                            rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(IpHelper.TcpRow)));
                        }
                    }
                }
                finally
                {
                    if (tcpTable != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(tcpTable);
                    }
                }
            }
        }

        #endregion
    }

    #endregion

    #region P/Invoke IP Helper API

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366073.aspx"/>
    /// </summary>
    public static class IpHelper
    {
        #region Public Fields

        public const string DllName = "iphlpapi.dll";
        public const int AfInet = 2;

        #endregion

        #region Public Methods

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa365928.aspx"/>
        /// </summary>
        [DllImport(IpHelper.DllName, SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int tcpTableLength, bool sort, int ipVersion, TcpTableType tcpTableType, int reserved);

        #endregion

        #region Public Enums

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366386.aspx"/>
        /// </summary>
        public enum TcpTableType
        {
            BasicListener,
            BasicConnections,
            BasicAll,
            OwnerPidListener,
            OwnerPidConnections,
            OwnerPidAll,
            OwnerModuleListener,
            OwnerModuleConnections,
            OwnerModuleAll,
        }

        #endregion

        #region Public Structs

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366921.aspx"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TcpTable
        {
            public uint Length;
            public TcpRow row;
        }

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366913.aspx"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TcpRow
        {
            public TcpState state;
            public uint localAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint remoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public int owningPid;
        }

        #endregion
    }

    #endregion
#endif
}
