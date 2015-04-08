using Sockets = System.Net.Sockets;
namespace Asterion.Exceptions {
    /**
     * Asterion base exception.
     */
     public class AsterionException : System.Exception {
        private Sockets.TcpClient tcpClient; //< The client which was involved in the exception.

        //! Gets the client involved in the exception.
        public Sockets.TcpClient Client {
            get { return tcpClient; }    
        }
        
        public AsterionException(Sockets.TcpClient client) : base() {
            tcpClient = client;
        }
        
        public AsterionException(string exceptionMessage, Sockets.TcpClient client) : base(exceptionMessage) {
            tcpClient = client;
        }
        
        public AsterionException(string exceptionMessage, AsterionException innerException, Sockets.TcpClient client) : base(exceptionMessage, innerException) {
            tcpClient = client;
        }
        
    }
}
