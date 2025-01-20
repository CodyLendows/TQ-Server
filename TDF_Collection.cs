using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    public class TDF_Collection
    {
        public Stream stream;
        public Client client;
        public byte[] buffer;

        public TDF_Collection(Stream stream, Client client, byte[] buffer) /* what's a TDF */
        {
            this.stream = stream;
            this.client = client;
            this.buffer = buffer;
        }
    }
}
