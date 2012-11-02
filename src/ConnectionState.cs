/**
 * @file    ConnectionState
 * @author  Lewis
 * @url     https://github.com/Lewis-H
 * @license http://www.gnu.org/copyleft/lesser.html
 */

namespace Asterion {
    using Sockets = System.Net.Sockets;

    /**
     * Holds information about a client and how to handle them.
     */
    public class ConnectionState {
        private int bufferLength; //< The amount of data to attempt to read on each read.
        private Sockets.TcpClient clientConnection; //< The TcpClient wrapper of the connection.
        private Sockets.NetworkStream clientStream; //< NetworkStream to send and receive from.
        private byte[] arrBuffer; //< Byte array of the received buffer.
        private string strBuffer = ""; //< String version of the buffer.

        //! Gets or sets the byte array from the received buffer.
        public byte[] Buffer {
            get { return arrBuffer; }
            set { arrBuffer = value; }
        }
        
        //! Gets or sets the buffer string.
        public string BufferString {
            get { return strBuffer; }
            set { strBuffer = value; }
        }
        
        //! Gets the client connection.
        public Sockets.TcpClient ClientConnection {
            get { return clientConnection; }
        }
        
        //! Gets the client network stream.
        public Sockets.NetworkStream ClientStream {
            get { return clientStream; }
        }
        
        //! Gets whether or not the client is connected.
        public bool Connected {
            get { return clientConnection.Connected; }
        }

        /**
         * Constructor for ConnectionState.
         *
         * @param newConnection
         *  The TcpClient to base the ConnectionState object on.
         * @param readLength
         *  The amount of data to attempt to read on each read.
         */
        public ConnectionState(Sockets.TcpClient newConnection, int readLength) {
            bufferLength = readLength;
            clientConnection = newConnection;
            clientStream = newConnection.GetStream();
            ClearBuffer();
        }
        
        /**
         * Clears the buffer array.
         */
        public void ClearBuffer() {
            Buffer = new byte[bufferLength];
        }
    }

}
