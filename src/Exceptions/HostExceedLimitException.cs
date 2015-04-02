namespace Asterion.Exceptions {
    using TcpClient = System.Net.Sockets.TcpClient;
    /**
     * Thrown when a remote host tries to make too many connections to the server.
     */
     public class HostExceedLimitException : AsterionException {
        public HostExceedLimitException(TcpClient client) : base(client) { }
        public HostExceedLimitException(string exceptionMessage, TcpClient client) : base(exceptionMessage, client) { }
        public HostExceedLimitException(string exceptionMessage, HostExceedLimitException innerException, TcpClient client) : base(exceptionMessage, innerException, client) { }
    }
}
