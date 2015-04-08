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
    using Interlocked = System.Threading.Interlocked;
    
    //! Recieve event handler.
    public delegate void ReceiveEventHandler(Connection Client, string packet);
    //! Connect event handler.
    public delegate void ConnectEventHandler(Connection Client);
    //! Disconnect event handler.
    public delegate void DisconnectEventHandler(Connection Client);
    //! Timeout event handler.
    public delegate void TimeoutEventHandler(Connection timeoutConnection, double time);

    /**
     * Implements a Transmission Control Protocol (TCP) server.
     */
    public class Server {
        private TcpListener listener; //< Socket to which the server will listen to for new connections.
        private string host; //< The host that the server will listen on.
        private int port; //< The port that the server will listen on.
        private int clients            = 0; //< The number of clients currently connected to the server.
        private int capacity           = 0; //< The maximum amount of clients allowed on the server.
        private int legalMaxBufferAge  = 0; //< The maximum legal time for a buffer since handling. The buffer will be cleared if over this age (0 is infinite).
        private int legalMaxBufferSize = 0; //< The maximum legal size of a buffer yet to be handled. The buffer will be cleared if over this size (0 is infinite).
        private double timeout = 0; //< Buffer timeout time.
        private bool started = false;
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
        }
        //! Gets or sets the amount of times to which an ip address may be connected to the server.
        public int IpLimit {
            get { return Limits.IpTable.Limit; }
            set { Limits.IpTable.Limit = value; }
        }
        //! Gets or sets the timeout time.
        public double Timeout {
            get { return timeout; }
        }
        //! Gets or sets the maximum legal buffer age (in milliseconds). 0 is infinite.
        public int MaximumLegalBufferAge {
            get { return legalMaxBufferAge; }
        }
        //! Gets or sets the maximum legal buffer size (in bytes). 0 is infinite.
        public int MaximumLegalBufferSize {
            get { return legalMaxBufferSize; }
        }
        
        /**
         * Starts up the server.
         *
         * @param startPort
         *  The port to listen to for new connections.
         */
        public Server(string host, int port, int capacity = 0, int maxBufferAge = 0, int timeout = 0, int maxBufferSize = 0, int ipLimit = 5) {
            this.host = host;
            this.port = port;
            this.timeout = timeout;
            this.legalMaxBufferAge = maxBufferAge;
            this.legalMaxBufferSize = maxBufferSize;
            if(capacity != 0) this.capacity = capacity;
            listener = new TcpListener(System.Net.IPAddress.Parse(host), port);
        }

        public void Start() {
            if(!started) {
                started = true;
                Listen();
            }
        }

        /**
         * Starts accepting connecting clients.
         */
        private void Listen() {
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
            bool reset = false;
            try {
                TcpClient client = listener.EndAcceptTcpClient(result);
                wait.Set();
                reset = true;
                client.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.KeepAlive, 1);
                Heard(client);
            }catch(System.Net.Sockets.SocketException ex) {
                // If this happens, socket error code information is at: http://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
                OnLog("Could not accept socket [" + ex.ErrorCode + "]: " + ex.Message);
            }catch(Exceptions.AsterionException ex) {
                // Either the server is full or the client has reached the maximum connections per IP.
                OnLog("Could not add client: " + ex.Message, Logging.LogLevel.Error);
                DisconnectClient(ex.Connection);                
            }finally{
                if(!reset) wait.Set();
            }
        }        

        /**
         * Handles a new client connecting to the server and starts reading from the client if the client limit has not been reached.
         *
         * @param client
         *  The new client.
         */
        private void Heard(TcpClient client) {
            Connection connection = new Connection {
                Client = client,
            };
            if(clients >= capacity && capacity != 0) throw new Exceptions.ServerFullException("Server full, rejecting client with IP '" + connection.Address + "'.", connection);
            Interlocked.Increment(ref clients);
            Limits.IpTable.Add(connection);
            if(timeout != 0) {
                connection.Timer.Interval = timeout;
                connection.Timer.Elapsed += OnTimeout;
                connection.Timer.Start();
            }
            OnConnect(connection);
            BeginRead(connection);
        }

        /**
         * Begin reading from a connected client.
         *
         * @param readConnection
         *  The Connection object of the client to read from.
         */
        private void BeginRead(Connection connection) {
            try {
                connection.Bytes = new byte[1024];
                lock(connection.SyncRoot)
                    if(connection.Connected) connection.Client.GetStream().BeginRead(connection.Bytes, 0, 1024, ReceiveCallback, connection);
            }catch(System.IO.IOException) {
                DisconnectHandler(connection);
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
            int read = 0;
            bool connected = false;
            int available = 0;
            lock(connection.SyncRoot)
                if(connection.Connected) {
                    read = EndRead(connection, result);
                    connected = connection.Client.Connected;
                    available = connection.Client.Available;
                }
            if(read != 0 && connected) {
                connection.Buffer += System.Text.Encoding.ASCII.GetString(connection.Bytes).Substring(0, read);
                if(read != 1024 && available == 0) {
                    OnReceive(connection, connection.Buffer);
                    connection.Buffer = "";
                }
                CheckStopwatch(connection);
                CheckBufferSize(connection);
                if(connection.Timer.Interval != timeout) connection.Timer.Interval = timeout;
                BeginRead(connection);
            }else{
                DisconnectHandler(connection);
            }
        }

        /**
         * End reading from a connected client.
         *
         * @param readConnection
         *  The Connection object of the client to stop reading from.
         * @param result
         *  The IAsyncResult returned from the asynchronous reading.
         */
        private int EndRead(Connection connection, System.IAsyncResult result) {
            try {
                lock(connection.SyncRoot)
                    if(connection.Connected)
                        return connection.Client.GetStream().EndRead(result);
                    else
                        return 0;
            }catch(System.IO.IOException) {
                return 0;
            }
        }

        /**
         * Handles the disconnection of a client from the server, calls the onDisconnect event.
         *
         * @param disconnectConnection
         *  The client that is disconnecting.
         */
        private void DisconnectHandler(Connection connection) {
            Interlocked.Decrement(ref clients);
            Limits.IpTable.Remove(connection);
            connection.Timer.Close();
            OnDisconnect(connection);
            connection.Client.Close();
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
        public bool WriteData(Connection connection, string data, bool isAsync) {
            try {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                lock(connection.SyncRoot) {
                    if(connection != null) {
                        if(isAsync) {
                            connection.Client.GetStream().BeginWrite(bytes, 0, data.Length, WriteCallback, connection);
                        }else{
                            connection.Client.GetStream().Write(bytes, 0, data.Length);
                        }
                        return connection.Client.GetStream().CanWrite;
                    }else{
                        return false;
                    }
                }
            }catch(System.IO.IOException ex) {
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
        public bool WriteData(Connection connection, string data) {
            return WriteData(connection, data, true);
        }

        /**
         * The write calback for when data has been sent to a client.
         *
         * @param result
         *  The IAsyncResult returned from the asynchronous writing.
         */
        private void WriteCallback(System.IAsyncResult result) {
            Connection connection = (Connection) result.AsyncState;
            try {
                lock(connection.SyncRoot)
                    if(connection.Connected) {
                        connection.Client.GetStream().EndWrite(result);
                        connection.Client.LingerState = new LingerOption(false, 0);
                    }
            }catch(System.IO.IOException ex) {
                OnLog("Could not end write to client: " + ex.Message + ".", Logging.LogLevel.Error);
            }
        }

        /**
         * Kicks a client off the server (Might leave the client with a sore butt).
         *
         * @param disconnectConnection
         *  The connection to close.
         */
        public void DisconnectClient(Connection connection) {
            try {
                lock(connection.SyncRoot)
                    if(connection.Connected) {
                        connection.Client.Client.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                        connection.Client.Close();
                    }
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
            if(legalMaxBufferSize != 0 && connection.Buffer.Length > legalMaxBufferSize) {
                OnLog("Client at host '" + connection.Address + "' has reached the maximum buffer size of " + legalMaxBufferSize.ToString() + " bytes, clearing buffer.", Logging.LogLevel.Warn);
                connection.Buffer = "";
                connection.Bytes = new byte[1024];
            }
         }
        
        /**
         * Handles the connection's stop watch. If the buffer was last handled over n milliseconds ago, the buffer is cleared.
         *
         * @param connection
         *  The connection to check.
         */
        private void CheckStopwatch(Connection connection) {
            if(connection.Buffer != "") {
                if(connection.Watch.IsRunning) {
                    if(legalMaxBufferAge != 0 && connection.Watch.Elapsed.TotalMilliseconds >= legalMaxBufferAge) {
                        OnLog("Client at host '" + connection.Address + "' has reached the maximum buffer age of " + legalMaxBufferAge.ToString() + " milliseconds, clearing buffer.", Logging.LogLevel.Warn);
                        connection.Bytes = new byte[1024];
                        connection.Buffer = "";
                        connection.Watch.Reset();
                    }
                }else{
                    connection.Watch.Start();
                }
            }else{
                connection.Watch.Stop();
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
            TimeoutEvent(connection, timer.Interval);
        }
        
        /**
         * Raises the connect event, which signals a new client has connected.
         *
         * @param client
         *  The new client.
         */
        private void OnConnect(Connection client) {
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
        private void OnReceive(Connection client, string packet) {
            if(ReceiveEvent != null) ReceiveEvent(client, packet);
        }
        
        /**
         * Raises the disconnect event, which sigals a client has disconnected from the server.
         *
         * @param client
         *  The client which disconnected.
         */
        private void OnDisconnect(Connection client) {
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
