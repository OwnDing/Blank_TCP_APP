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
using Blank_TCP_Server.Methods.AsyncServer;
using Blank_TCP_Server.Methods.AsyncAwaitServer;
using Microsoft.VisualBasic;

namespace Blank_TCP_Server
{
    public partial class tcpform : Form
    {
        //private const Int32 DEFAULT_NUM_CONNECTIONS = 1005, DEFAULT_BUFFER_SIZE = Int16.MaxValue;
        //SocketListener sl;
        //bool start;

        //AsyncTcpServer ats;
        AsyncAwaitServer aas;

        public tcpform()
        {
            InitializeComponent();
            txtipport.Text = Properties.Settings.Default.ipport;
            txtTimer.Text = Properties.Settings.Default.beeptime;
        }
        #region btn
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

        private void btn_timersend_Click(object sender, EventArgs e)
        {
            if (txtSendData.Text == string.Empty)
            {
                MessageBox.Show("please input something in above textbox！");
                return;
            }
            if (btn_run.Text == "Stop")
            {
                if (timer1.Enabled == false)
                {
                    Console.WriteLine("timer started!");
                    timer1.Interval = Convert.ToInt32(txtTimer.Text);
                    timer1.Enabled = true;
                    return;
                }          
            }
            Console.WriteLine("timer stoped!");
            timer1.Enabled = false;
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
                //start = true;
                btn_run.Text = "Stop";
                var th = new Thread(run);
                th.Start();
                txtipport.Enabled = false;
                txtTimer.Enabled = false;
                txtMaxConnections.Enabled = false;
            }
            else
            {
                //start = false;
                Console.WriteLine("please enter \"exit\" to stop server in the console!");
                btn_run.Text = "Start";
                txtipport.Enabled = true;
                txtTimer.Enabled = true;
                txtMaxConnections.Enabled = true;
                //lvClients.Clear();
            }
            
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
            //sl.sendfromsocket(data);
            //ats.SyncSendToAll(Encoding.ASCII.GetBytes(data));
            aas.SendToAll(Encoding.ASCII.GetBytes(data));
        }
        #endregion

        #region start server
        private void run()
        {
            try
            {
                int port = Convert.ToInt32(txtipport.Text);
                int max = Convert.ToInt32(txtMaxConnections.Text);
                aas = new AsyncAwaitServer(port, max);
                string msg = "Server listening on port " + port.ToString() + "...\n";
                Console.ForegroundColor = (ConsoleColor)((60 - 1) % 16);
                Console.WriteLine(msg);
                msg = "Timer Invert:" + timer1.Interval;
                Console.WriteLine(msg);
                aas.eventlistview += UpdateListView;
                aas.run();                                
                //while (start)
                //{

                //}
                aas.isStop = true;
                Console.WriteLine("Server has Stoped!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        //private void run()
        //{
        //    try
        //    {
        //        int port = Convert.ToInt32(txtipport.Text);
        //        ats = new AsyncTcpServer(port);
        //        ats.Start();
        //        string msg = "Server listening on port " + port.ToString() + "...\n";
        //        Console.ForegroundColor = (ConsoleColor)((60 - 1) % 16);
        //        Console.WriteLine(msg);
        //        msg = "Timer Invert:" + timer1.Interval;
        //        Console.WriteLine(msg);
        //        ats.eventlistview += UpdateListView;
        //        while (start)
        //        {

        //        }
        //        ats.Stop();
        //        Console.WriteLine("Server has Stoped!");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.ToString());
        //    }
        //}
        //private void run()// SocketAsyncEventArgs 
        //{
        //    try
        //    {
        //        Int32 port = Convert.ToInt32(txtipport.Text);
        //        Int32 bufferSize = DEFAULT_BUFFER_SIZE;

        //        sl = new SocketListener(DEFAULT_NUM_CONNECTIONS, bufferSize);
        //        sl.Start(port);
        //        string msg = "Server listening on port " + port.ToString() + ". Press any key to terminate the server process...\n";
        //        Console.ForegroundColor = (ConsoleColor)((60 - 1) % 16);
        //        Console.WriteLine(msg);
        //        msg = "Timer Invert:" + timer1.Interval;
        //        Console.WriteLine(msg);
        //        sl.eventlistview += UpdateListView;
        //        while (start)
        //        {

        //        }
        //        sl.Stop();
        //        sl.closeallclients = true;
        //        sl = null;
        //        Console.WriteLine("Server stoped!\n");
        //    }
        //    catch (IndexOutOfRangeException)
        //    {
        //        PrintUsage();
        //    }
        //    catch (FormatException)
        //    {
        //        PrintUsage();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.ToString());
        //    }
        //}

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: SocketAsyncServer <port> [numConnections] [bufferSize].");
            Console.WriteLine("\t<port> Numeric value for the listening TCP port.");
            Console.WriteLine("\t[numConnections] Numeric value for the maximum number of incoming connections.");
            Console.WriteLine("\t[bufferSize] Numeric value for the buffer size of incoming connections.");
        }
        #endregion

        #region send
        private void timer1_Tick(object sender, EventArgs e)
        {           
            try
            {
                string data = txtSendData.Text;
                //sl.sendfromsocket(data);
                //ats.SyncSendToAll(Encoding.ASCII.GetBytes(data));
                aas.SendToAll(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void tsmiSend_Click(object sender, EventArgs e)
        {
            var selectedItems =lvClients.SelectedItems;
            if (selectedItems.Count > 0)
            {
                string datagram = Microsoft.VisualBasic.Interaction.InputBox("please input something", "SendToTcpClient", string.Empty, -1, -1);
                for (int i = 0; i < selectedItems.Count; i++)
                {
                    string ip = selectedItems[i].SubItems[1].Text + ":" + selectedItems[i].SubItems[2].Text;
                    aas.SendToSelectedClient(ip, datagram);
                }
            }

        }
        #endregion
        #region UpdateListView From Socket
        private void UpdateListView(string ip,string status)
        {
            lvClients.Invoke((MethodInvoker)delegate {
                if (status.ToUpper() == "C")
                {
                    ListViewItem lvi;
                    lvi = new ListViewItem(" Connected", 0);
                    var values = ip.Split(':');
                    lvi.SubItems.Add(ip.Substring(0, ip.Length - values[values.Count() - 1].Length - 1));
                    lvi.SubItems.Add(values[values.Count() - 1]);
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
