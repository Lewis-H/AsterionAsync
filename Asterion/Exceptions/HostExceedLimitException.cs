namespace Asterion.Exceptions {
    /**
     * Thrown when a remote host tries to make too many connections to the server.
     */
     public class HostExceedLimitException : AsterionException {
        public HostExceedLimitException(Connection connection) : base(connection) { }
        public HostExceedLimitException(string exceptionMessage, Connection connection) : base(exceptionMessage, connection) { }
        public HostExceedLimitException(string exceptionMessage, HostExceedLimitException innerException, Connection connection) : base(exceptionMessage, innerException, connection) { }
    }
}
