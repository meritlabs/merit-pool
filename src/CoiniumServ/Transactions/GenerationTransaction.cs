#region License
//
//     MIT License
//
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//
//     Copyright (C) 2013 - 2017, CoiniumServ Project
//     Copyright (C) 2017 - 2018 The Merit Foundation
//
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//     SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoiniumServ.Coin.Coinbase;
using CoiniumServ.Cryptology;
using CoiniumServ.Daemon;
using CoiniumServ.Daemon.Responses;
using CoiniumServ.Jobs;
using CoiniumServ.Pools;
using CoiniumServ.Transactions.Script;
using CoiniumServ.Utils.Extensions;
using CoiniumServ.Utils.Helpers;
using Gibbed.IO;
using Serilog;

namespace CoiniumServ.Transactions
{
    // TODO: convert generation transaction to ioc & DI based.

    /// <summary>
    /// A generation transaction.
    /// </summary>
    /// <remarks>
    /// * It has exactly one txin.
    /// * Txin's prevout hash is always 0000000000000000000000000000000000000000000000000000000000000000.
    /// * Txin's prevout index is 0xFFFFFFFF.
    /// More info:  http://bitcoin.stackexchange.com/questions/20721/what-is-the-format-of-coinbase-transaction
    ///             http://bitcoin.stackexchange.com/questions/21557/how-to-fully-decode-a-coinbase-transaction
    ///             http://bitcoin.stackexchange.com/questions/4990/what-is-the-format-of-coinbase-input-scripts
    /// </remarks>
    /// <specification>
    /// https://en.bitcoin.it/wiki/Protocol_specification#tx
    /// https://en.bitcoin.it/wiki/Transactions#Generation
    /// </specification>
    public class GenerationTransaction : IGenerationTransaction
    {
        /// <summary>
        /// Transaction data format version
        /// </summary>
        public UInt32 Version { get; private set; }


        /// <summary>
        /// Coinbase tx signature script
        /// </summary>
        public SignatureScript CoinbaseSignatureScript { get; private set; }

        /// <summary>
        ///  For coins that support/require transaction comments
        /// </summary>
        public byte[] TxMessage { get; private set; }

        /// <summary>
        /// The block number or timestamp at which this transaction is locked:
        ///                 0 	        Always locked
        ///  LESS THEN      500000000 	Block number at which this transaction is locked
        ///  EQUAL GREATER  500000000 	UNIX timestamp at which this transaction is locked
        /// </summary>
        public UInt32 LockTime { get; private set; }

        /// <summary>
        /// Part 1 of the generation transaction.
        /// </summary>
        public byte[] Initial { get; private set; }

        /// <summary>
        /// Part 2 of the generation transaction.
        /// </summary>
        public byte[] Final { get; private set; }

        public IBlockTemplate BlockTemplate { get; private set; }

        public IExtraNonce ExtraNonce { get; private set; }

        public IPoolConfig PoolConfig { get; private set; }

        private readonly ILogger _logger;


        /// <summary>
        /// Creates a new instance of generation transaction.
        /// </summary>
        /// <param name="extraNonce">The extra nonce.</param>
        /// <param name="blockTemplate">The block template.</param>
        /// <param name="poolConfig">The associated pool's configuration</param>
        /// <remarks>
        /// Reference implementations:
        /// https://github.com/zone117x/node-stratum-pool/blob/b24151729d77e0439e092fe3a1cdbba71ca5d12e/lib/transactions.js
        /// https://github.com/Crypto-Expert/stratum-mining/blob/master/lib/coinbasetx.py
        /// </remarks>
        public GenerationTransaction(IExtraNonce extraNonce, IBlockTemplate blockTemplate, IPoolConfig poolConfig)
        {
            _logger = Log.ForContext<GenerationTransaction>();

            BlockTemplate = blockTemplate;
            ExtraNonce = extraNonce;
            PoolConfig = poolConfig;

            Version = blockTemplate.Version;
            TxMessage = Serializers.SerializeString(poolConfig.Meta.TxMessage);
            LockTime = 0;

            CoinbaseSignatureScript =
                new SignatureScript(
                    blockTemplate.Height,
                    blockTemplate.CoinBaseAux.Flags,
                    TimeHelpers.NowInUnixTimestamp(),
                    (byte) extraNonce.ExtraNoncePlaceholder.Length,
                    "/MeritPool/");

        }

        public void Create()
        {
            string originalSignatireScriptHex;
            using (var stream = new MemoryStream())
            {
                var heightBytes = Serializers.SerializeNumber(BlockTemplate.Height);
                stream.WriteByte((byte) (heightBytes.Length + 1));
                stream.WriteBytes(heightBytes);
                stream.WriteByte(0);

                originalSignatireScriptHex = stream.ToArray().ToHexString();
            }

            var coinbaseTxData = BlockTemplate.Transactions[0].Data;
            var placeholderPos = coinbaseTxData.IndexOf(originalSignatireScriptHex) + originalSignatireScriptHex.Length;

            string[] tokens = coinbaseTxData.Split(new[] { originalSignatireScriptHex }, StringSplitOptions.None);

            // create the first part.
            using (var stream = new MemoryStream())
            {
                stream.WriteBytes(tokens[0].HexToByteArray()); // write version
                // write signature
                var signatureScriptLenght = (UInt32)(CoinbaseSignatureScript.Initial.Length + ExtraNonce.ExtraNoncePlaceholder.Length + CoinbaseSignatureScript.Final.Length);
                stream.WriteBytes(Serializers.VarInt(signatureScriptLenght).ToArray());
                stream.WriteBytes(CoinbaseSignatureScript.Initial);

                Initial = stream.ToArray();
            }

            /*  The generation transaction must be split at the extranonce (which located in the transaction input
                scriptSig). Miners send us unique extranonces that we use to join the two parts in attempt to create
                a valid share and/or block. */

            // create the second part.
            using (var stream = new MemoryStream())
            {
                stream.WriteBytes(CoinbaseSignatureScript.Final);
                stream.WriteBytes(tokens[1].HexToByteArray());

                if (PoolConfig.Coin.Options.TxMessageSupported)
                    stream.WriteBytes(TxMessage);

                Final = stream.ToArray();
            }
        }
    }
}
