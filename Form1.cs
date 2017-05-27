using Blank_TCP_Server.Methods;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace Blank_TCP_Server
{
    public partial class tcpform : Form
    {
        private const Int32 DEFAULT_NUM_CONNECTIONS = 40, DEFAULT_BUFFER_SIZE = Int16.MaxValue;
        SocketListener sl;
        bool start;

        public tcpform()
        {
            InitializeComponent();
            txtipport.Text = Properties.Settings.Default.ipport;
            txtTimer.Text = Properties.Settings.Default.beeptime;
        }

        private void btn_setting_Click(object sender, EventArgs e)
        {
            if (txtipport.Text.Length < 3 || txtTimer.Text=="")
            {
                MessageBox.Show("請設置正確的IP,PORT,Timer！");
                return;
            }

            Properties.Settings.Default.ipport = txtipport.Text;
            Properties.Settings.Default.beeptime = txtTimer.Text;
            Properties.Settings.Default.Save();
        }

        private void btn_run_Click(object sender, EventArgs e)
        {
            if (txtipport.Text.Length < 3 )
            {
                MessageBox.Show("請設置正確的IP和TIME！");
                return;
            }

            if (btn_run.Text == "Start")
            {
                start = true;
                btn_run.Text = "Stop";
                var th = new Thread(run);
                th.Start();
                txtipport.Enabled = false;
                txtTimer.Enabled = false;
                timer1.Interval = Convert.ToInt32(txtTimer.Text);
                timer1.Enabled = true;
            }
            else
            {
                start = false;
                btn_run.Text = "Start";
                txtipport.Enabled = true;
                txtTimer.Enabled = true;
                timer1.Enabled = false;
                //lvClients.Clear();
            }
            
        }

        private void run()
        {
            try
            {
                Int32 port = Convert.ToInt32(txtipport.Text) ;
                Int32 bufferSize = DEFAULT_BUFFER_SIZE;

                sl = new SocketListener(DEFAULT_NUM_CONNECTIONS, bufferSize);
                sl.Start(port);
                string msg = "Server listening on port "+port.ToString()+". Press any key to terminate the server process...\n";
                Console.ForegroundColor = (ConsoleColor)((60 - 1) % 16);
                Console.WriteLine(msg);
                msg = "Timer Invert:" + timer1.Interval;
                Console.WriteLine(msg);
                sl.eventlistview += UpdateListView;
                while (start)
                {

                }                
                sl.Stop();
                Console.WriteLine("Server stoped!\n");
            }
            catch (IndexOutOfRangeException)
            {
                PrintUsage();
            }
            catch (FormatException)
            {
                PrintUsage();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: SocketAsyncServer <port> [numConnections] [bufferSize].");
            Console.WriteLine("\t<port> Numeric value for the listening TCP port.");
            Console.WriteLine("\t[numConnections] Numeric value for the maximum number of incoming connections.");
            Console.WriteLine("\t[bufferSize] Numeric value for the buffer size of incoming connections.");
        }

        private void btn_Send_Click(object sender, EventArgs e)
        {
            string data;
            if (txtSendData.Text != string.Empty)
            {
                data = txtSendData.Text;
            }
            else
            {
                data = "hello from server!";
            }
            sl.sendfromsocket(data);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                string data = "1";
                sl.sendfromsocket(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        #region UpdateListView From Socket
        private void UpdateListView(string ip,string status)
        {
            //Console.ForegroundColor = (ConsoleColor)((90 - 1) % 16);
            //Console.WriteLine(ip + "---" + status);
            //Console.ForegroundColor = (ConsoleColor)((60 - 1) % 16);
            lvClients.Invoke((MethodInvoker)delegate {
                if (status.ToUpper() == "C")
                {
                    ListViewItem lvi;
                    lvi = new ListViewItem(" Connected", 0);
                    var values = ip.Split(':');
                    lvi.SubItems.Add(values[0]);
                    lvi.SubItems.Add(values[1]);
                    lvClients.Items.Add(lvi);
                }
                else
                {
                    for (int i = lvClients.Items.Count - 1; i >= 0; i--)
                    {
                        string s=lvClients.Items[i].SubItems[1].Text+":" + lvClients.Items[i].SubItems[2].Text;
                        if (s==ip)
                        {
                            lvClients.Items[i].Remove();
                        }
                    }
                }
            });
        }

        #endregion
    }
}
