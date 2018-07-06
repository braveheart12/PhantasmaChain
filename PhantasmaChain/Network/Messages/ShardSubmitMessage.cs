﻿using Phantasma.Core;
using Phantasma.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network
{
    internal class ShardSubmitMessage : Message
    {
        public readonly uint ShardID;
        public IEnumerable<Transaction> Transactions => _transactions;

        private Transaction[] _transactions;

        public ShardSubmitMessage(uint shardID, IEnumerable<Transaction> transactions)
        {
            this.ShardID = shardID;
            this._transactions = transactions.ToArray();
        }

        internal static Message FromReader(BinaryReader reader)
        {
            var shardID = reader.ReadUInt32();
            var txCount = reader.ReadUInt16();
            var txs = new Transaction[txCount];
            for (int i=0; i<txCount; i++)
            {
                var tx = Transaction.Unserialize(reader);
            }

            return new ShardSubmitMessage(shardID, txs);
        }
    }
}