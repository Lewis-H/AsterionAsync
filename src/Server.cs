/**
 * @file    Server
 * @author  Lewis
 * @url     https://github.com/Lewis-H
 * @license http://www.gnu.org/copyleft/lesser.html
 */

/**
 * The Asterion namespace provides a simple to use TCP server base, which is extensible to function as many kinds of server applications.
 */
namespace Asterion {
    using TcpClient = System.Net.Sockets.TcpClient;
    using TcpListener = System.Net.Sockets.TcpListener;
    using LingerOption = System.Net.Sockets.LingerOption;
    using ManualResetEvent = System.Threading.ManualResetEvent;
    using ElapsedEventArgs = System.Timers.ElapsedEventArgs;
    
    //! Recieve event handler.
    public delegate void ReceiveEventHandler(TcpClient Client, string packet);
    //! Connect event handler.
    public delegate void ConnectEventHandler(TcpClient Client);
    //! Disconnect event handler.
    public delegate void DisconnectEventHandler(TcpClient Client);
    //! Timeout event handler.
    public delegate void TimeoutEventHandler(TcpClient timeoutConnection, double time);

    /**
     * Implements a Transmission Control Protocol (TCP) server.
     */
    public class Server {
        private TcpListener listener; //< Socket to which the server will listen to for new connections.
        private string host; //< The host that the server will listen on.
        private int port; //< The port that the server will listen on.
        private int clients            = 0; //< The number of clients currently connected to the server.
        private int readLength         = 1024; //< The number of bytes to read from the buffer. You may want to lower this if you feel most packets won't be 1024 bytes in length.
        private int capacity           = 0; //< The maximum amount of clients allowed on the server.
        private int legalMaxBufferAge  = 0; //< The maximum legal time for a buffer since handling. The buffer will be cleared if over this age (0 is infinite).
        private int legalMaxBufferSize = 0; //< The maximum legal size of a buffer yet to be handled. The buffer will be cleared if over this size (0 is infinite).
        private string delimiter = "\0"; //< The delimiter to separate packets.
        private double timeout = 0; //< Timeout time.
        public event ReceiveEventHandler ReceiveEvent; //< Event raised when a packet is recieved.
        public event ConnectEventHandler ConnectEvent; //< Event raised when a new client has connected.
        public event DisconnectEventHandler DisconnectEvent; //< Event raised when a client has disconnected.
        public event Logging.LogEventHandler LogEvent; //< Event raised when the logger is being written to.
        public event TimeoutEventHandler TimeoutEvent; //< Event raised when a client has timed out.

        //! Gets the host which the server is listening on.
        public string Host {
            get { return host; }
        }
        //! Gets the port number which the server is listening to.
        public int Port {
            get { return port; }
        }
        //! Gets the amount of clients currently connected to the server.
        public int Count {
            get { return clients; }
        }
        //! Sets the maximum amount of clients allowed on the server.
        public int Capacity {
            get { return capacity; }
            set { capacity = value; }
        }
        //! Gets or sets the delimiter string.
        public string Delimiter {
            get { return delimiter; }
            set { delimiter = value; }
        }
        //! Gets or sets the amount of times to which an ip address may be connected to the server.
        public int IpLimit {
            get { return Limits.IpTable.Limit; }
            set { Limits.IpTable.Limit = value; }
        }
        //! Gets or sets the timeout time.
        public double Timeout {
            get { return timeout; }
            set { timeout = value; }
        }
        //! Gets or sets the maximum legal buffer age (in milliseconds). 0 is infinite.
        public int MaximumLegalBufferAge {
            get { return legalMaxBufferAge; }
            set { legalMaxBufferAge = value; }
        }
        //! Gets or sets the maximum legal buffer size (in bytes). 0 is infinite.
        public int MaximumLegalBufferSize {
            get { return legalMaxBufferSize; }
            set { legalMaxBufferSize = value; }
        }
        
        /**
         * Starts up the server.
         *
         * @param startPort
         *  The port to listen to for new connections.
         */
        public void Start(string host, int port, int capacity = 0) {
            this.host = host;
            this.port = port;
            if(capacity != 0) this.capacity = capacity;
            listener = new TcpListener(System.Net.IPAddress.Parse(host), port);
            InitialiseAccept();
        }

        /**
         * Starts accepting connecting clients.
         */
        private void InitialiseAccept() {
            listener.Start();
            OnLog("Awaiting connections...");
            ManualResetEvent wait = new ManualResetEvent(false);
            while(true) {
                wait.Reset();
                listener.BeginAcceptTcpClient(AcceptCallback, wait);
                wait.WaitOne();
            }
        }


        /**
         * The accept callback for when a client has connected, calls the onConnect event.
         *
         * @param result
         *  The IAsyncResult returned from the asynchronous accept.
         */
        private void AcceptCallback(System.IAsyncResult result) {
            ManualResetEvent wait = (ManualResetEvent) result.AsyncState;
            try {
                TcpClient client = listener.EndAcceptTcpClient(result);
                client.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.KeepAlive, 1);
                AddClient(client);
            }catch(System.Net.Sockets.SocketException ex) {
                // If this happens, socket error code information is at: http://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
                OnLog("Could not accept socket [" + ex.ErrorCode + "]: " + ex.Message);
            }catch(Exceptions.AsterionException ex) {
                // Either the server is full or the client has reached the maximum connections per IP.
                OnLog("Could not add client: " + ex.Message, Logging.LogLevel.Error);
                DisconnectClient(ex.Client);                
            }finally{
                wait.Set();
            }
        }        

        /**
         * Handles a new client connecting to the server and starts reading from the client if the client limit has not been reached.
         *
         * @param client
         *  The new client.
         */
        private void AddClient(TcpClient client) {
            Connection connection = new Connection(client, readLength);
            if(clients >= capacity && capacity != 0) throw new Exceptions.ServerFullException("Server full, rejecting client with IP '" + connection.Address + "'.", client);
            clients++;
            Limits.IpTable.Add(connection);
            connection.Timer.Interval = timeout;
            if(connection.Timer.Interval != 0) {
                connection.Timer.Elapsed += OnTimeout;
                connection.Timer.Start();
            }
            OnConnect(client);
            BeginRead(connection);
        }

        /**
         * Begin reading from a connected client.
         *
         * @param readConnection
         *  The Connection object of the client to read from.
         */
        private void BeginRead(Connection readConnection) {
            try {
                readConnection.Stream.BeginRead(readConnection.Buffer.TemporaryBuffer, 0, readConnection.Buffer.Size, ReceiveCallback, readConnection);
            }catch(System.SystemException ex) {
                if(ex is System.IO.IOException == false && ex is System.ObjectDisposedException == false) throw;
                DisconnectHandler(readConnection);
            }
        }

        /**
         * The receive callback for when data has been received from a client, calls the onReceive event.
         *
         * @param result
         *  The IAsyncResult returned from the asynchronous reading.
         */ 
        private void ReceiveCallback(System.IAsyncResult result) {
            Connection connection = (Connection) result.AsyncState;
            int read = EndRead(connection, result);
            if(read > 0 && connection.Connected) {
                connection.Buffer.BufferString += Utils.BytesToStr(connection.Buffer.TemporaryBuffer).Substring(0, read);
                connection.Buffer.Clear();
                HandleData(connection);
                BeginRead(connection);
            }else{
                DisconnectHandler(connection);
            }
        }

        /**
         * Handles data sent by a connection. Recursively reads the received data between delimiters.
         *
         * @param connection
         *  The connection that we have recieved data from.
         */
        private void HandleData(Connection connection) {
            int split;
            while((split = connection.Buffer.BufferString.IndexOf(delimiter)) != -1) {
                string packet;
                packet = connection.Buffer.BufferString.Substring(0, split);
                OnReceive(connection.Client, packet);
                if(connection.Connected == false) return;
                connection.Buffer.BufferString = connection.Buffer.BufferString.Substring(split + delimiter.Length);
                connection.Buffer.Watch.Restart();
                connection.Timer.Restart();
            }
            CheckStopwatch(connection);
            CheckBufferSize(connection);
            if(connection.Timer.Interval != timeout) connection.Timer.Interval = timeout;
        }

        /**
         * End reading from a connected client.
         *
         * @param readConnection
         *  The Connection object of the client to stop reading from.
         * @param result
         *  The IAsyncResult returned from the asynchronous reading.
         */
        private int EndRead(Connection readConnection, System.IAsyncResult result) {
            try {
                return readConnection.Stream.EndRead(result);
            }catch(System.Exception ex) {
                if(ex is System.IO.IOException == false && ex is System.ObjectDisposedException == false) throw;
                return 0;
            }
        }

        /**
         * Handles the disconnection of a client from the server, calls the onDisconnect event.
         *
         * @param disconnectConnection
         *  The client that is disconnecting.
         */
        private void DisconnectHandler(Connection disconnectConnection) {
            clients--;
            Limits.IpTable.Remove(disconnectConnection);
            disconnectConnection.Timer.Close();
            OnDisconnect(disconnectConnection.Client);
            disconnectConnection.Client.Close();
        }

        /**
         * Writes data to a client.
         *
         * @param writeConnection
         *  The client to write data to.
         * @param sendData
         *  The data to send to the client.
         * @param isAsync
         *  Whether the action is asynchronous or not.
         */
        public bool WriteData(TcpClient connection, string sendData, bool isAsync) {
            try {
                byte[] data = Utils.StrToBytes(sendData + delimiter);
                if(isAsync) {
                    connection.GetStream().BeginWrite(data, 0, data.Length, WriteCallback, connection);
                }else{
                    connection.GetStream().Write(data, 0, data.Length);
                }
                return connection.GetStream().CanWrite;
            }catch(System.Exception ex) {
                if(ex is System.IO.IOException == false && ex is System.ObjectDisposedException == false) throw;
                OnLog("Could not end write to client: " + ex.Message + ".", Logging.LogLevel.Error);
                return false;
            }
        }
        
        /**
         * Writes data to a client with default options (asynchronous).
         *
         * @param writeConnection
         *  The client to write data to.
         * @param sendData
         *  The data to send to the client.
         */
        public bool WriteData(TcpClient writeConnection, string sendData) {
            return WriteData(writeConnection, sendData, true);
        }

        /**
         * The write calback for when data has been sent to a client.
         *
         * @param result
         *  The IAsyncResult returned from the asynchronous writing.
         */
        private void WriteCallback(System.IAsyncResult result) {
            TcpClient writeConnection = (TcpClient) result.AsyncState;
            try {
                writeConnection.GetStream().EndWrite(result);
                writeConnection.LingerState = new LingerOption(false, 0);
            }catch(System.Exception ex) {
                if(ex is System.IO.IOException == false && ex is System.ObjectDisposedException == false) throw;
                OnLog("Could not end write to client: " + ex.Message + ".", Logging.LogLevel.Error);
            }
        }

        /**
         * Kicks a client off the server (Might leave the client with a sore butt).
         *
         * @param disconnectConnection
         *  The connection to close.
         */
        public void DisconnectClient(TcpClient disconnectConnection) {
            try {
                disconnectConnection.Client.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                disconnectConnection.Close();
            }catch(System.Exception e) {
                OnLog("Could not disconnect socket: " + e.Message, Asterion.Logging.LogLevel.Error);
            }
        }

        
        /**
         * Handles the size of the buffer. If the buffer is too large, it is cleared.
         *
         * @param connection
         *  The connection to check.
         */
         private void CheckBufferSize(Connection connection) {
            if(legalMaxBufferSize != 0 && connection.Buffer.BufferString.Length > legalMaxBufferSize) {
                OnLog("Client at host '" + connection.Address + "' has reached the maximum buffer size of " + legalMaxBufferSize.ToString() + " bytes, clearing buffer.", Logging.LogLevel.Warn);
                connection.Buffer.Clear();
                connection.Buffer.BufferString = "";
            }
         }
        
        /**
         * Handles the connection's stop watch. If the buffer was last handled over n milliseconds ago, the buffer is cleared.
         *
         * @param connection
         *  The connection to check.
         */
        private void CheckStopwatch(Connection connection) {
            if(connection.Buffer.BufferString != "") {
                if(connection.Buffer.Watch.IsRunning) {
                    System.Console.WriteLine("Buffer age: " + connection.Buffer.Watch.Elapsed.TotalMilliseconds + " milliseconds.");
                    if(legalMaxBufferAge != 0 && connection.Buffer.Watch.Elapsed.TotalMilliseconds >= legalMaxBufferAge) {
                        OnLog("Client at host '" + connection.Address + "' has reached the maximum buffer age of " + legalMaxBufferAge.ToString() + " milliseconds, clearing buffer.", Logging.LogLevel.Warn);
                        connection.Buffer.Clear();
                        connection.Buffer.BufferString = "";
                        connection.Buffer.Watch.Reset();
                    }
                }else{
                    connection.Buffer.Watch.Start();
                }
            }else{
                connection.Buffer.Watch.Stop();
            }
        }

        /**
         * Timer elapse event handler, raised only when the client has timed out.
         *
         * @param source
         *  The sending object.
         * @param e
         *  The elapsed event arguments.
         */
        private void OnTimeout(object source, ElapsedEventArgs e) {
            Limits.TimeoutTimer timer = (Limits.TimeoutTimer) source;
            timer.Stop();
            Connection connection = (Connection) timer.Tag;
            TimeoutEvent(connection.Client, timer.Interval);
        }
        
        /**
         * Raises the connect event, which signals a new client has connected.
         *
         * @param client
         *  The new client.
         */
        private void OnConnect(TcpClient client) {
            if(ConnectEvent != null) ConnectEvent(client);
        }
        
        /**
         * Raises the receive event, which signals a packet has been received from a client.
         *
         * @param client
         *  The client we received data from.
         * @param packet
         *  The packet received.
         */
        private void OnReceive(TcpClient client, string packet) {
            if(ReceiveEvent != null) ReceiveEvent(client, packet);
        }
        
        /**
         * Raises the disconnect event, which sigals a client has disconnected from the server.
         *
         * @param client
         *  The client which disconnected.
         */
        private void OnDisconnect(TcpClient client) {
            if(DisconnectEvent != null) DisconnectEvent(client);
        }

        /**
         * Raises the log event.
         *
         * @param text
         *  The text to log.
         * @param level
         *  The log level.
         */
        public void OnLog(string text, Logging.LogLevel level) {
            if(LogEvent != null) LogEvent(text, level);
        }

        /**
         * Raises the log event.
         *
         * @param text
         *  The text to log.
         */
        public void OnLog(string text) {
            OnLog(text, Logging.LogLevel.Info);
        }
    }

}
