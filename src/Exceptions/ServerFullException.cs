namespace Asterion.Exceptions {
    using TcpClient = System.Net.Sockets.TcpClient;
    /**
     * Thrown when the server is full.
     */
     public class ServerFullException : AsterionException {        
        public ServerFullException(TcpClient client) : base(client) { }
        public ServerFullException(string exceptionMessage, TcpClient client) : base(exceptionMessage, client) { }
        public ServerFullException(string exceptionMessage, ServerFullException innerException, TcpClient client) : base(exceptionMessage, innerException, client) { }
    }
}
