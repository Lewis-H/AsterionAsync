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
    using Diagnostics = System.Diagnostics;


    public class Connection {
        private Limits.TimeoutTimer timer;
        private Diagnostics.Stopwatch watch = new Diagnostics.Stopwatch();
        internal readonly object SyncRoot = new object();
        private TcpClient client;

        /// <summary>
        /// The buffer, where 
        /// </summary>
        /// <value>The buffer.</value>
        internal string Buffer {
            get;
            set;
        }

        internal byte[] Bytes {
            get;
            set;
        }

        internal Limits.TimeoutTimer Timer {
            get { return timer; }
        }

        internal TcpClient Client {
            get { return client; }
            set { 
                Address = (value.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
                client = value;
            }

        }

        internal Diagnostics.Stopwatch Watch {
            get { return watch; }
        }
        
        public string Address {
            get;
            private set;
        }

        public bool Connected {
            get { 
                lock(SyncRoot)
                    return (Client != null && Client.Connected);
            }
        }

        public object Tag {
            get;
            set;
        }

        public Connection() {
            timer = new Limits.TimeoutTimer(this);
        }
    }
        

}
