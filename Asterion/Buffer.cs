namespace Asterion {
    using Stopwatch = System.Diagnostics.Stopwatch;

    class Buffer {

        private byte[] tempBuffer; //< The buffer array.
        private int size; //< The size of the buffer.
        private string bufferString; //< The buffer string.
        private Stopwatch stopwatch;

        //! Gets or sets the byte array for the temporary buffer.
        public byte[] TemporaryBuffer {
            get { return tempBuffer; }
            set { tempBuffer = value; }
        }

        //! Gets the buffer size.
        public int Size {
            get { return size; }
        }

        //! Gets or sets the buffer string.
        public string BufferString {
            get { return bufferString; }
            set { bufferString = value; }
        }

        public Stopwatch Watch {
            get { return stopwatch; }
        }

        /**
         * Initiates a new buffer.
         *
         * @param length
         *  The length of the buffer array.
         */
        public Buffer(int length) {
            size = length;
            Clear();
        }

        /**
         * Clears the buffer array.
         */
        public void Clear() {
            tempBuffer = new byte[size];
            stopwatch = new Stopwatch();
        }
    }
}
