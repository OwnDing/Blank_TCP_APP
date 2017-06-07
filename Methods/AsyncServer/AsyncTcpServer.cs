using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Blank_TCP_Server.Methods.AsyncServer
{
    public class AsyncTcpServer : IDisposable
    {
        #region Fields

        private TcpListener _listener;
        private ConcurrentDictionary<string, TcpClientState> _clients;
        private bool _disposed = false;

        /// <summary>
        /// The total number of clients connected to the server.
        /// </summary>
        private Int32 numConnectedSockets=0;
        /// <summary>
        /// tell the main form to delete or add connected info
        /// </summary>
        private string list_D = "D";
        private string list_C = "C";

        #endregion

        #region Ctors

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="listenPort">监听的端口</param>
        public AsyncTcpServer(int listenPort)
            : this(IPAddress.Any, listenPort)
        {
        }

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localEP">监听的终结点</param>
        public AsyncTcpServer(IPEndPoint localEP)
            : this(localEP.Address, localEP.Port)
        {
        }

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localIPAddress">监听的IP地址</param>
        /// <param name="listenPort">监听的端口</param>
        public AsyncTcpServer(IPAddress localIPAddress, int listenPort)
        {
            Address = localIPAddress;
            Port = listenPort;
            this.Encoding = Encoding.UTF8;
            this.numConnectedSockets = 0;
            _clients = new ConcurrentDictionary<string, TcpClientState>();

            _listener = new TcpListener(Address, Port);
            _listener.AllowNatTraversal(true);
        }

        #endregion

        #region Properties

        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 监听的IP地址
        /// </summary>
        public IPAddress Address { get; private set; }
        /// <summary>
        /// 监听的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 通信使用的编码
        /// </summary>
        public Encoding Encoding { get; set; }

        #endregion

        #region Server

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>异步TCP服务器</returns>
        public AsyncTcpServer Start()
        {
            return Start(10);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">服务器所允许的挂起连接序列的最大长度</param>
        /// <returns>异步TCP服务器</returns>
        public AsyncTcpServer Start(int backlog)
        {
            if (IsRunning) return this;

            IsRunning = true;

            _listener.Start(backlog);
            ContinueAcceptTcpClient(_listener);

            return this;
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns>异步TCP服务器</returns>
        public AsyncTcpServer Stop()
        {
            if (!IsRunning) return this;

            try
            {
                _listener.Stop();
                IsRunning = false;
                foreach (var client in _clients.Values)
                {
                    client.TcpClient.Client.Disconnect(true);
                    //Interlocked.Decrement(ref this.numConnectedSockets);
                    //Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", this.numConnectedSockets);
                    UpdateListView(client.IP, list_D);
                }
                _clients.Clear();
            }
            catch (ObjectDisposedException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
            catch (SocketException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }

            

            return this;
        }

        private void ContinueAcceptTcpClient(TcpListener tcpListener)
        {
            try
            {
                tcpListener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), tcpListener);
            }
            catch (ObjectDisposedException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
            catch (SocketException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
        }

        #endregion

        #region Receive

        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (!IsRunning) return;

            try
            {
                TcpListener tcpListener = (TcpListener)ar.AsyncState;

                TcpClient tcpClient = tcpListener.EndAcceptTcpClient(ar);
                if (!tcpClient.Connected) return;
                
                byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                TcpClientState internalClient = new TcpClientState(tcpClient, buffer);

                // add client connection to cache
                string tcpClientKey = internalClient.TcpClient.Client.RemoteEndPoint.ToString();
                _clients.AddOrUpdate(tcpClientKey, internalClient, (n, o) => { return internalClient; });
                RaiseClientConnected(tcpClient);
                Interlocked.Increment(ref this.numConnectedSockets);
                Console.WriteLine("Client connection accepted. There are {0} clients connected to the server",
                    this.numConnectedSockets);
                UpdateListView(internalClient.IP, list_C);
                // begin to read data
                NetworkStream networkStream = internalClient.NetworkStream;
                ContinueReadBuffer(internalClient, networkStream);

                // keep listening to accept next connection
                ContinueAcceptTcpClient(tcpListener);
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine("Program does not terminate normally! But Can be used normally.Error:ObjectDisposedExceptio");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void HandleDatagramReceived(IAsyncResult ar)
        {
            if (!IsRunning) return;
            TcpClientState internalClient = (TcpClientState)ar.AsyncState;
            try
            {              
                if (!internalClient.TcpClient.Connected) {
                    Interlocked.Decrement(ref this.numConnectedSockets);
                    Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", this.numConnectedSockets);
                    UpdateListView(internalClient.IP, list_D);
                    return; }

                NetworkStream networkStream = internalClient.NetworkStream;

                int numberOfReadBytes = 0;
                try
                {
                    // if the remote host has shutdown its connection, 
                    // read will immediately return with zero bytes.
                    numberOfReadBytes = networkStream.EndRead(ar);
                }
                catch (Exception ex)
                {
                    //ExceptionHandler.Handle(ex);
                    Console.WriteLine("A remote host has closed connection!！---" + internalClient.IP);
                    numberOfReadBytes = 0;
                }

                if (numberOfReadBytes == 0)
                {
                    // connection has been closed
                    TcpClientState internalClientToBeThrowAway;
                    string tcpClientKey = internalClient.IP;
                    _clients.TryRemove(tcpClientKey, out internalClientToBeThrowAway);
                    RaiseClientDisconnected(internalClient.TcpClient);
                    //Console.WriteLine("Number:"+_clients.Count());
                    Interlocked.Decrement(ref this.numConnectedSockets);
                    Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", this.numConnectedSockets);
                    UpdateListView(tcpClientKey, list_D);
                    return;
                }

                // received byte and trigger event notification
                var receivedBytes = new byte[numberOfReadBytes];               
                Array.Copy(internalClient.Buffer, 0, receivedBytes, 0, numberOfReadBytes);

                string msg = "Recevied:" + Encoding.ASCII.GetString(receivedBytes) + "  From:" + internalClient.IP;
                Console.WriteLine(msg);
                
                RaiseDatagramReceived(internalClient.TcpClient, receivedBytes);
                //RaisePlaintextReceived(internalClient.TcpClient, receivedBytes);

                // continue listening for tcp datagram packets
                ContinueReadBuffer(internalClient, networkStream);
            }
            catch (InvalidOperationException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
        }

        private void ContinueReadBuffer(TcpClientState internalClient, NetworkStream networkStream)
        {
            try
            {
                networkStream.BeginRead(internalClient.Buffer, 0, internalClient.Buffer.Length, HandleDatagramReceived, internalClient);
            }
            catch (ObjectDisposedException ex)
            {
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
        }

        #endregion

        #region Events

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

        /// <summary>
        /// 接收到数据报文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<byte[]>> DatagramReceived;
        /// <summary>
        /// 接收到数据报文明文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<string>> PlaintextReceived;
       
        private void RaiseDatagramReceived(TcpClient sender, byte[] datagram)
        {
            if (DatagramReceived != null)
            {
                DatagramReceived(this, new TcpDatagramReceivedEventArgs<byte[]>(sender, datagram));
                Console.WriteLine("Recevied:"+Encoding.ASCII.GetString(datagram));
            }
        }

        private void RaisePlaintextReceived(TcpClient sender, byte[] datagram)
        {
            if (PlaintextReceived != null)
            {
                PlaintextReceived(this, new TcpDatagramReceivedEventArgs<string>(sender, this.Encoding.GetString(datagram, 0, datagram.Length)));
                Console.WriteLine("Recevied:" + this.Encoding.GetString(datagram, 0, datagram.Length));
            }
        }

        /// <summary>
        /// 与客户端的连接已建立事件
        /// </summary>
        public event EventHandler<TcpClientConnectedEventArgs> ClientConnected;
        /// <summary>
        /// 与客户端的连接已断开事件
        /// </summary>
        public event EventHandler<TcpClientDisconnectedEventArgs> ClientDisconnected;

        private void RaiseClientConnected(TcpClient tcpClient)
        {
            if (ClientConnected != null)
            {
                ClientConnected(this, new TcpClientConnectedEventArgs(tcpClient));
            }
        }

        private void RaiseClientDisconnected(TcpClient tcpClient)
        {
            if (ClientDisconnected != null)
            {
                ClientDisconnected(this, new TcpClientDisconnectedEventArgs(tcpClient));
            }
        }

        #endregion

        #region Send

        private void GuardRunning()
        {
            if (!IsRunning)
                throw new InvalidProgramException("This TCP server has not been started yet.");
        }

        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        public void Send(TcpClient tcpClient, byte[] datagram)
        {
            GuardRunning();

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
        public void Send(TcpClient tcpClient, string datagram)
        {
            Send(tcpClient, this.Encoding.GetBytes(datagram));
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SendToAll(byte[] datagram)
        {
            GuardRunning();

            foreach (var client in _clients.Values)
            {
                Send(client.TcpClient, datagram);
            }
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SendToAll(string datagram)
        {
            GuardRunning();

            SendToAll(this.Encoding.GetBytes(datagram));
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
                //ExceptionHandler.Handle(ex);
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        public void SyncSend(TcpClient tcpClient, byte[] datagram)
        {
            GuardRunning();

            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");

            if (datagram == null)
                throw new ArgumentNullException("datagram");

            try
            {
                NetworkStream stream = tcpClient.GetStream();
                if (stream.CanWrite)
                {
                    stream.Write(datagram, 0, datagram.Length);
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
        public void SyncSend(TcpClient tcpClient, string datagram)
        {
            SyncSend(tcpClient, this.Encoding.GetBytes(datagram));
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SyncSendToAll(byte[] datagram)
        {
            GuardRunning();

            foreach (var client in _clients.Values)
            {
                SyncSend(client.TcpClient, datagram);
            }
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SyncSendToAll(string datagram)
        {
            GuardRunning();

            SyncSendToAll(this.Encoding.GetBytes(datagram));
            //SyncSendToAll(Encoding.UTF8.GetBytes(datagram));
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();

                        if (_listener != null)
                        {
                            _listener = null;
                        }
                    }
                    catch (SocketException ex)
                    {
                        //ExceptionHandler.Handle(ex);
                        Console.WriteLine(ex.ToString());
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
