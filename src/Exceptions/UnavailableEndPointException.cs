namespace Asterion.Exceptions {
    using TcpClient = System.Net.Sockets.TcpClient;
    /**
     * Thrown when we cannot get a client's address.
     */
     public class UnavailableEndPointException : AsterionException {        
        public UnavailableEndPointException(TcpClient client) : base(client) { }
        public UnavailableEndPointException(string exceptionMessage, TcpClient client) : base(exceptionMessage, client) { }
        public UnavailableEndPointException(string exceptionMessage, UnavailableEndPointException innerException, TcpClient client) : base(exceptionMessage, innerException, client) { }
    }
}
