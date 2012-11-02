namespace AsterionExample {
    using Sockets = System.Net.Sockets;

    public class Program {
        public static void Main() {
            // Makes a new instance of ExampleServer.
            ExampleServer myServer = new ExampleServer();
            // Starts the ExampleServer.
            myServer.Start();
        }
    }

    public class ExampleServer {
        private Asterion.Server server;

        public ExampleServer() {
            server = new Asterion.Server();
        }

        public void Start() {
            // Sets the onConnect event to ConnectHandler.
            server.onConnect = ConnectHandler;
            // Sets the onReceive event to ReceiveHandler.
            server.onReceive = ReceiveHandler;
            // Sets the onDisconnect event to DisconnectHandler
            server.onDisconnect = DisconnectHandler;
            // Sets the delimeter to "\r\n".
            server.Delimiter = "\r\n";
            // Starts the server on port 9000.
            server.Start(9000);
        }

        // Method that is called when the onConnect event is triggered.
        public void ConnectHandler(Sockets.TcpClient handleConnection) {
            Asterion.Out.Logger.WriteOutput("New client connected.");
        }

        // Method that is called when the onReceive event is triggered.
        public void ReceiveHandler(Sockets.TcpClient handleConnection, string strData) {
            Asterion.Out.Logger.WriteOutput("Received: " + strData);
            // Sends the same data back to the client.
            server.WriteData(handleConnection, strData);
        }

        // Method that is called when the onDisconnect event is triggered.
        public void DisconnectHandler(Sockets.TcpClient handleConnection) {
            Asterion.Out.Logger.WriteOutput("Client has disconnected.");
        }
    }
}
