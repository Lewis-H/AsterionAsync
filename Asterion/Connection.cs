/**
 * @file    ConnectionState
 * @author  Lewis
 * @url     https://github.com/Lewis-H
 * @license http://www.gnu.org/copyleft/lesser.html
 */

namespace Asterion {
    using TcpClient = System.Net.Sockets.TcpClient;
    using NetworkStream = System.Net.Sockets.NetworkStream;
    using IPEndPoint = System.Net.IPEndPoint;

    /**
     * Holds information about a client and how to handle them.
     */
    class Connection {
        private TcpClient client; //< The TcpClient wrapper of the connection.
        private NetworkStream stream; //< NetworkStream to send and receive from.
        private Buffer buffer; // Buffer storage.
        private IPEndPoint endPoint; //< Client's IP end point.
        private Limits.TimeoutTimer timer; //< Timeout timer, elapses if the client does not send data in a given amount of time.

        //! Gets the client's buffer.
        public Buffer Buffer {
            get { return buffer; }
        }

        //! Gets the client connection.
        public TcpClient Client {
            get { return client; }
        }
        
        //! Gets the client network stream.
        public NetworkStream Stream {
            get { return stream; }
        }
        
        //! Gets whether or not the client is connected.
        public bool Connected {
            get { return client.Connected; }
        }
        
        //! Gets the address of this connection.
        public string Address {
            get { return endPoint.Address.ToString(); }
        }

        //! Gets the timeout timer of this connection.
        public Limits.TimeoutTimer Timer {
            get { return timer; }
        }
        
        /**
         * Constructor for Connection.
         *
         * @param newConnection
         *  The TcpClient to base the ConnectionState object on.
         * @param readLength
         *  The amount of data to attempt to read on each read.
         */
        public Connection(TcpClient connection, int readLength) {
            buffer = new Buffer(readLength);
            client = connection;
            stream = connection.GetStream();
            timer = new Limits.TimeoutTimer(this);
            endPoint = connection.Client.RemoteEndPoint as System.Net.IPEndPoint;
            if(endPoint == null) throw new Exceptions.UnavailableEndPointException("Could not get the client's IP end point!", connection);
        }
        
    }

}
