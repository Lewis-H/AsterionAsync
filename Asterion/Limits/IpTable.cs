/**
 * @file    IpTable
 * @author  Lewis
 * @url     https://github.com/Lewis-H
 * @license http://www.gnu.org/copyleft/lesser.html
 */

namespace Asterion.Limits {
    using GCollections = System.Collections.Generic;
    using TcpClient = System.Net.Sockets.TcpClient;

    /**
     * Records the number of times an IP address is connected to the server.
     */
    static class IpTable {
        private static GCollections.Dictionary<string, int> ipDictionary; //< Dictionary containing ip addresses and the amount of times they are connected.
        private static object dictionaryLock; //< Dictionary lock.
        private static int limit; //< The amount of times to which a single IP may be connected to the server.
        
        //! Gets or sets the amount of times to which a single IP may be connected to the server.
        public static int Limit {
            get { return limit; }
            set { limit = value; }
        }
        
        /**
         * IpLimiter constructor.
         */
        static IpTable() {
            ipDictionary = new GCollections.Dictionary<string, int>();
            dictionaryLock = new object();
            limit = 5;
        }
        
        /**
         * Adds one to the count of the given ip address.
         *
         * @param address
         *  The ip address.
         */ 
        public static void Add(Connection connection) {
            string address = connection.Address;
            lock(dictionaryLock) {
                if(ipDictionary.ContainsKey(address)) {
                    if(limit != 0 && CountOf(address) >= limit) throw new Exceptions.HostExceedLimitException("The host '" + address + "' is at the connection limit.", connection.Client);
                    ipDictionary[address]++;
                }else{
                    ipDictionary[address] = 1;
                }
            }
        }

        /**
         * Determines where an ip address has reached the connection limit.
         *
         * @param address
         *  The ip address.
         */
        public static bool ReachedLimit(Connection connection) {
            return (CountOf(connection.Address) >= limit || limit == 0);
        }
        
        /**
         * Counts the amount of times an ip address is connected.
         *
         * @param address
         *  The ip address.
         */
        public static int CountOf(string address) {
            try{
                return ipDictionary[address];
            }catch{
                return 0;
            }
        }

        /**
         * Removes one from the count of the given ip address.
         *
         * @param address
         *  The ip address.
         */ 
        public static void Remove(Connection connection) {
            string address = connection.Address;
            lock(dictionaryLock) {
                if(ipDictionary.ContainsKey(address)) {
                    if(CountOf(address) <= 1) {
                        ipDictionary.Remove(address);
                    }else{
                        ipDictionary[address]--;
                    }
                }
            }    
        }
        
    }
}
