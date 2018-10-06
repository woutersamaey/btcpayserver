using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;

namespace BTCPayServer.AtomicSwaps
{
    public class Preimage
    {
        public Preimage()
        {
            Bytes = RandomUtils.GetBytes(32);
        }
        public Preimage(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes
        {
            get; set;
        }

        public uint160 GetHash()
        {
            return new uint160(Hashes.Hash160(Bytes, Bytes.Length));
        }
    }
}
