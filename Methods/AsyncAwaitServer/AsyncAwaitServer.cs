using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Blank_TCP_Server.Methods.AsyncAwaitServer
{
    public class AsyncAwaitServer
    {
        private Int32 numConnectedSockets;
        CancellationTokenSource cts;
        TcpListener listener;
        private bool isRuning;
        private Int32 maxConnectedClients;
        public bool isStop = false;
        private ConcurrentDictionary<string, TcpClient> _clients;
        /// <summary>
        /// tell the main form to delete or add connected info
        /// </summary>
        private string list_D = "D";
        private string list_C = "C";

        /// <summary>
        /// 更新主界面TCP鏈接信息
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="status"></param>
        public delegate void ChangeListView(string ip, string status);
        public event ChangeListView eventlistview;
        private void UpdateListView(string ip, string status)
        {
            if (eventlistview != null)
                eventlistview(ip, status);
        }


        public AsyncAwaitServer(int port, int maxConnectedClients)
        {
            cts = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Any, port);
            this.numConnectedSockets = 0;
            this.maxConnectedClients = maxConnectedClients;
            _clients = new ConcurrentDictionary<string, TcpClient>();
        }
        public void run()
        {           
            try
            {
                listener.Start();
                Console.WriteLine("please enter \"exit\" to stop server");
                //just fire and forget. We break from the "forgotten" async loops
                //in AcceptClientsAsync using a CancellationToken from `cts`
                isRuning = true;
                var task=AcceptClientsAsync(listener, cts.Token);
                if (task.IsFaulted)
                    task.Wait();
                //Thread.Sleep(60000); //block here to hold open the server
                while (!isStop)
                {
                    string msg = Console.ReadLine();
                    if (msg == "exit")
                        break;
                }
                cts.Cancel();
                listener.Stop();
            }
            finally
            {
                cts.Cancel();
                listener.Stop();
            }

            foreach (var client in _clients.Values)
            {
                client.Client.Disconnect(true);
                UpdateListView(client.Client.RemoteEndPoint.ToString(), list_D);
            }
            _clients.Clear();
        }

        private void reStartListener()
        {
            isRuning = true;
            listener.Start();
            var task = AcceptClientsAsync(listener, cts.Token);
        }

        async Task AcceptClientsAsync(TcpListener listener, CancellationToken ct)
        {
            var ip = string.Empty;
            while (!ct.IsCancellationRequested)
            {
                if (numConnectedSockets >= this.maxConnectedClients)
                {
                    isRuning = false;
                    listener.Stop();
                    break;
                }
                TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                ip = client.Client.RemoteEndPoint.ToString();
                //once again, just fire and forget, and use the CancellationToken
                //to signal to the "forgotten" async invocation.
                var task= EchoAsync(client, ip, ct);
            }
        }

        

        async Task EchoAsync(TcpClient client,
                     string clientIndex,
                     CancellationToken ct)
        {
            Console.WriteLine("New client ({0}) connected", clientIndex);
            string ip = client.Client.RemoteEndPoint.ToString();
            using (client)
            {
                var buf = new byte[4096];
                var stream = client.GetStream();
                if (!client.Client.Connected) return;
                Interlocked.Increment(ref this.numConnectedSockets);
                Console.WriteLine("Client connection accepted. There are {0} clients connected to the server",
                    this.numConnectedSockets);
                _clients.AddOrUpdate(client.Client.RemoteEndPoint.ToString(), client, (n, o) => { return client; });
                UpdateListView(client.Client.RemoteEndPoint.ToString(), list_C);
                while (!ct.IsCancellationRequested)
                {
                    //under some circumstances, it's not possible to detect
                    //a client disconnecting if there's no data being sent
                    //so it's a good idea to give them a timeout to ensure that 
                    //we clean them up.
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(250));
                    var amountReadTask = stream.ReadAsync(buf, 0, buf.Length, ct);
                    
                    var completedTask = await Task.WhenAny(timeoutTask, amountReadTask)
                                                  .ConfigureAwait(false);
                    if (completedTask == timeoutTask)
                    {
                        var msg = Encoding.ASCII.GetBytes("Client timed out");
                        await stream.WriteAsync(msg, 0, msg.Length);
                        break;
                    }
                    //now we know that the amountTask is complete so
                    //we can ask for its Result without blocking
                    if (amountReadTask.IsFaulted || amountReadTask.IsCanceled) break;
                    var amountRead = amountReadTask.Result;

                    Console.WriteLine(Encoding.ASCII.GetString(buf, 0, amountRead) + "---From:" + ip);
                    if (amountRead == 0) break; //end of stream.
                    //await stream.WriteAsync(buf, 0, amountRead, ct)
                    //            .ConfigureAwait(false);
                }
            }
            Interlocked.Decrement(ref this.numConnectedSockets);
            Console.WriteLine("Client ({0}) disconnected.There are {1} clients connected to the server", clientIndex,numConnectedSockets);
            _clients.TryRemove(ip, out client);
            UpdateListView(ip, list_D);
            if (numConnectedSockets < this.maxConnectedClients&&isRuning==false)
            {
                reStartListener();
            }
        }

        #region send
        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        private void Send(TcpClient tcpClient, byte[] datagram)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");

            if (datagram == null)
                throw new ArgumentNullException("datagram");

            try
            {
                NetworkStream stream = tcpClient.GetStream();
                if (stream.CanWrite)
                {
                    stream.BeginWrite(datagram, 0, datagram.Length, HandleDatagramWritten, tcpClient);
                }
            }
            catch (ObjectDisposedException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        private void Send(TcpClient tcpClient, string datagram)
        {
            Send(tcpClient, Encoding.ASCII.GetBytes(datagram));
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SendToAll(byte[] datagram)
        {
            foreach (var client in _clients.Values)
            {
                Send(client, datagram);
            }
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SendToAll(string datagram)
        {
            SendToAll(Encoding.ASCII.GetBytes(datagram));
        }
        /// <summary>
        /// 发送报文到指定的客户端
        /// </summary>
        /// <param name="ip">客户端IP</param>
        /// <param name="datagram">信息</param>
        public void SendToSelectedClient(string ip,string datagram)
        {
            Send(_clients[ip], datagram);
        }

        private void HandleDatagramWritten(IAsyncResult ar)
        {
            try
            {
                ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
            }
            catch (ObjectDisposedException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
            catch (InvalidOperationException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion
    }
}
