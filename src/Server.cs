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
    using Sockets = System.Net.Sockets;
    using Threading = System.Threading;
    
    //! The recieve event handler delegate.
    public delegate void ReceiveEventHandler(Sockets.TcpClient clientConnection, string strPacket);
    //! The connect event handler delegate.
    public delegate void ConnectEventHandler(Sockets.TcpClient clientConnection);
    //! The disconnect event handler delegate.
    public delegate void DisconnectEventHandler(Sockets.TcpClient clientConnection);

    /**
     * Implements a Transmission Control Protocol (TCP) server.
     */
    public class Server {
        private Sockets.TcpListener connectionListener; //< Socket to which the server will listen to for new connections.
        private int listeningPort  = 0; //< The port that the server will listen on.
        private int currentClients = 0; //< The number of clients currently connected to the server.
        private int readLength     = 1024; //< The number of bytes to read from the buffer. You may want to lower this if you feel most packets won't be 1024 bytes in length.
        private int maximumClients = 0; //< The maximum amount of clients allowed on the server.
        private string delimiterString = "\0"; //< The delimiter to separate packets (e.g. "MESSAGE: fred likes bananas.\0MESSAGE: bananas like fred.\0").
        private Threading.ManualResetEvent acceptBlock = new Threading.ManualResetEvent(false); //< Manual reset event to pause the thread whilst waiting for the current socket to be accpted.
        private event ReceiveEventHandler receiveEvent; //< Event called when a packet is recieved.
        private event ConnectEventHandler connectEvent; //< Event called when a new client has connected.
        private event DisconnectEventHandler disconnectEvent; //< Event called when a client has disconnected.

        //! Gets the master socket for the server.
        public Sockets.TcpListener Sock {
            get { return connectionListener; }
        }
        //! Gets the port number which the server is listening to.
        public int Port {
            get { return listeningPort; }
        }
        //! Gets the amount of clients currently connected to the server.
        public int ClientCount {
            get { return currentClients; }
        }
        //! Gets or sets the receive event. 
        public ReceiveEventHandler onReceive {
            set { receiveEvent = value; }
            get { return receiveEvent; }
        }
        //! Gets or sets the connect event. 
        public ConnectEventHandler onConnect {
            set { connectEvent = value; }
            get { return connectEvent; }
        }
        //! Gets or sets the disconnect event. 
        public DisconnectEventHandler onDisconnect {
            set { disconnectEvent = value; }
            get { return disconnectEvent; }
        }
        //! Gets or sets the delimiter string.
        public string Delimiter {
            get { return delimiterString; }
            set { delimiterString = value; }
        }

        /**
         * Starts up the server.
         *
         * @param startPort
         *  The port to listen to for new connections.
         */
        public void Start(int startPort) {
            listeningPort = startPort;
            try {
                connectionListener = new Sockets.TcpListener(System.Net.IPAddress.Parse("0"), startPort);
                InitialiseAccept();
            }catch(Sockets.SocketException sckEx) {
                Out.Logger.WriteOutput("Could not bind to port. Error [" + sckEx.ErrorCode.ToString() + "] " + sckEx.Message + ".", Out.Logger.LogLevel.Error);
            }
        }
        
        /**
         * Starts up the server.
         *
         * @param startPort
         *  The port to listen to for new connections.
         * @param maxClients
         *  The maximum amount of clients that can be connected to the server at any one time.
         */
        public void Start(int startPort, int maxClients) {
            maximumClients = maxClients;
            Start(startPort);
        }

        /**
         * Begin reading from a connected client.
         *
         * @param readConnection
         *  The ConnectionState object of the client to read from.
         */
        private void BeginRead(ConnectionState readConnection) {
            if(readConnection.ClientStream.CanRead) {
                try {
                readConnection.ClientStream.BeginRead(readConnection.Buffer, 0, readLength, ReceiveCallback, readConnection);
                }catch{
                    DisconnectClient(readConnection.ClientConnection);
                }
            }else{
                DisconnectHandler(readConnection);
            }
        }

        /**
         * End reading from a connected client.
         *
         * @param readConnection
         *  The ConnectionState object of the client to stop reading from.
         * @param asyncResult
         *  The IAsyncResult returned from the asynchronous reading.
         */
        private int EndRead(ConnectionState readConnection, System.IAsyncResult asyncResult) {
            try {
                return readConnection.ClientStream.EndRead(asyncResult);
            }catch(System.Exception readEx) {
                Asterion.Out.Logger.WriteOutput("Could not end read: " + readEx.Message + ".", Asterion.Out.Logger.LogLevel.Error);
                return 0;
            }
        }

        /**
         * Writes data to a client.
         *
         * @param writeConnection
         *  The client to write data to.
         * @param sendData
         *  The data to send to the client.
         */
        public void WriteData(Sockets.TcpClient writeConnection, string sendData) {
            try {
                byte[] arrWrite = Utils.StrToBytes(sendData + delimiterString);
                writeConnection.LingerState = new Sockets.LingerOption(true, 2);
                if(writeConnection.GetStream().CanWrite) writeConnection.GetStream().BeginWrite(arrWrite, 0, arrWrite.Length, WriteCallback, writeConnection);
            }catch(System.Exception writeEx){
                Out.Logger.WriteOutput("Could not write to client: " + writeEx.Message + ".");
            }
        }

        /**
         * Kicks a client off the server (Might leave the client with a sore butt).
         *
         * @param disconnectConnection
         *  The connection to close.
         */
        public void DisconnectClient(Sockets.TcpClient disconnectConnection) {
            if(disconnectConnection.Client.Connected) {
                disconnectConnection.Client.Shutdown(Sockets.SocketShutdown.Both);
                disconnectConnection.Client.Disconnect(true);
            }
        }

        /**
         * Starts accepting connecting clients.
         */
        private void InitialiseAccept() {
            connectionListener.Start();
            Out.Logger.WriteOutput("Awaiting connections...");
            while(true) {
                acceptBlock.Reset();
                connectionListener.BeginAcceptTcpClient(AcceptCallback, null);
                acceptBlock.WaitOne();
            }
        }


        /**
         * The accept callback for when a client has connected, calls the onConnect event.
         *
         * @param asyncResult
         *  The IAsyncResult returned from the asynchronous accept.
         */
        private void AcceptCallback(System.IAsyncResult asyncResult) {
            try {
                Sockets.TcpClient newConnection = connectionListener.EndAcceptTcpClient(asyncResult);
                acceptBlock.Set();
                HandleNewClient(newConnection);
            }catch(System.Net.Sockets.SocketException sckEx) {
                acceptBlock.Set();
                Out.Logger.WriteOutput("Could not accept socket: " + sckEx.Message + ". ");
                return;
            }
        }

        /**
         * The receive callback for when data has been received from a client, calls the onReceive event.
         *
         * @param asyncResult
         *  The IAsyncResult returned from the asynchronous reading.
         */ 
        private void ReceiveCallback(System.IAsyncResult asyncResult) {
            ConnectionState handleConnection = (ConnectionState) asyncResult.AsyncState;
            int bytesRead = EndRead(handleConnection, asyncResult);
            if(bytesRead > 0 & handleConnection.Connected) {
                handleConnection.BufferString += Utils.BytesToStr(handleConnection.Buffer).Substring(0, bytesRead);
                handleConnection.ClearBuffer();
                HandleData(handleConnection);
            }else{
                DisconnectHandler(handleConnection);
            }
        }

        /**
         * The write calback for when data has been sent to a client.
         *
         * @param asyncResult
         *  The IAsyncResult returned from the asynchronous writing.
         */
        private void WriteCallback(System.IAsyncResult asyncResult) {
            Sockets.TcpClient writeConnection = (Sockets.TcpClient) asyncResult.AsyncState;
            try {
                writeConnection.GetStream().EndWrite(asyncResult);
                writeConnection.LingerState = new Sockets.LingerOption(false, 0);
            }catch(System.Exception writeEx) {
                Out.Logger.WriteOutput("Unable to end writing: " + writeEx.Message);
            }
        }

        /**
         * Handles a new client connecting to the server and starts reading from the client if the client limit has not been reached.
         *
         * @param newConnection
         *  The new client.
         */
        private void HandleNewClient(Sockets.TcpClient newConnection) {
            if(currentClients <= maximumClients || maximumClients == 0) {
                currentClients++;
                if(connectEvent != null) connectEvent(newConnection);
                ConnectionState connectionState = new ConnectionState(newConnection, readLength);
                BeginRead(connectionState);
            }else{
                Out.Logger.WriteOutput("Server full, rejecting client.", Out.Logger.LogLevel.Warn);
                newConnection.Client.Shutdown(Sockets.SocketShutdown.Both);
                newConnection.Client.Disconnect(true);
            }
        }

        /**
         * Handles data sent by a connection. Recursively reads the received data between delimiters.
         *
         * @param handleConnection
         *  The connection that we have recieved data from.
         */
        private void HandleData(ConnectionState handleConnection) {
            int splitPos = handleConnection.BufferString.IndexOf(delimiterString);
            if(handleConnection.Connected & splitPos != -1) {
                string strPacket = handleConnection.BufferString.Substring(0, splitPos);
                if(receiveEvent != null) receiveEvent(handleConnection.ClientConnection, strPacket);
                handleConnection.BufferString = handleConnection.BufferString.Substring(splitPos + delimiterString.Length);
                HandleData(handleConnection);
            }else if(handleConnection.Connected == false) {
                DisconnectHandler(handleConnection);
            }else if(splitPos == -1) {
                BeginRead(handleConnection);
            }
        }

        /**
         * Handles the disconnection of a client from the server, calls the onDisconnect event.
         *
         * @param disconnectConnection
         *  The client that is disconnecting.
         */
        private void DisconnectHandler(ConnectionState disconnectConnection) {
                currentClients--;
                if(disconnectEvent != null) disconnectEvent(disconnectConnection.ClientConnection);
                disconnectConnection.ClientConnection.Client.Disconnect(true);
                disconnectConnection = null;
        }

    }

}
