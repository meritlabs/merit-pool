﻿#region License
//
//     MIT License
//
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//
//     Copyright (C) 2013 - 2017, CoiniumServ Project
//     Copyright (C) 2017 - 2019 The Merit Foundation
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
using CoiniumServ.Utils.Extensions;

namespace CoiniumServ.Jobs
{
    /// <summary>
    /// Counter for extra nonce.
    /// Hex-encoded, per-connection unique string which will be used for coinbase serialization later. (http://mining.bitcoin.cz/stratum-mining)
    /// </summary>
    public class ExtraNonce:IExtraNonce
    {
        // TODO: use ioc for extranonce too

        private UInt32 _counter;

        /// <summary>
        /// ExtraNonce placeholder to be used with coinbase transactions.
        /// </summary>
        public byte[] ExtraNoncePlaceholder { get; private set; }

        /// <summary>
        /// The number of bytes that the miner users for its ExtraNonce2 counter 
        /// <remarks>Represents expected length of extranonce2 which will be generated by the miner. (http://mining.bitcoin.cz/stratum-mining)</remarks>
        /// </summary>
        public const int ExpectedExtraNonce2Size = 0x4;

        public ExtraNonce(UInt32 instanceId)
        {
            ExtraNoncePlaceholder = "f000000ff111111f".HexToByteArray();
            InitCounter(instanceId); // init. the extra nonce counter.
        }


        /// <summary>
        /// Inits ExtraNonce counter based on current instance Id.
        /// </summary>
        private void InitCounter(UInt32 instanceId)
        {
            _counter = instanceId << 27;  // init the ExtraNonce counter - last 5 most-significant bits represents instanceId, the rest is just an iterator of jobs.
        }

        /// <summary>
        /// Returns the next extranonce.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Last 5 most-significant bits represents instanceId, the rest is just an iterator of jobs.
        /// Basically allows us to run more-than-one pool-nodes within the same database.
        /// More: https://github.com/moopless/stratum-mining-litecoin/issues/23#issuecomment-22728564
        /// </remarks>
        public UInt32 Next()
        {
            return ++_counter; // return the next extra-nonce.
        }
    }
}
