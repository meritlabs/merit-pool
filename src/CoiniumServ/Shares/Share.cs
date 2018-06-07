#region License
//
//     MIT License
//
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//
//     Copyright (C) 2013 - 2017, CoiniumServ Project
//     Copyright (C) 2017-2018 The Merit Foundation
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
using System.IO;
using System.Text;
using CoiniumServ.Algorithms;
using CoiniumServ.Coin.Coinbase;
using CoiniumServ.Cryptology;
using CoiniumServ.Daemon.Responses;
using CoiniumServ.Jobs;
using CoiniumServ.Mining;
using CoiniumServ.Server.Mining.Stratum;
using CoiniumServ.Utils.Extensions;
using CoiniumServ.Utils.Helpers;
using CoiniumServ.Utils.Numerics;
using Konscious.Security.Cryptography;
using Gibbed.IO;
using Serilog;

namespace CoiniumServ.Shares
{
    public class Share : IShare
    {
        public bool IsValid { get { return Error == ShareError.None; } }
        public bool IsBlockCandidate { get; private set; }
        public Block Block { get; private set; }
        public Transaction GenerationTransaction { get; private set; }
        public bool IsBlockAccepted { get { return Block != null; } }
        public IMiner Miner { get; private set; }
        public ShareError Error { get; private set; }
        public UInt64 JobId { get; private set; }
        public IJob Job { get; private set; }
        public int Height { get; private set; }
        public UInt32 NTime { get; private set; }
        public UInt32 Nonce { get; private set; }
        public UInt32 ExtraNonce1 { get; private set; }
        public UInt32 ExtraNonce2 { get; private set; }
        public byte[] CoinbaseBuffer { get; private set; }
        public Hash CoinbaseHash { get; private set; }
        public byte[] MerkleRoot { get; private set; }
        public byte[] HeaderBuffer { get; private set; }
        public byte[] HeaderHash { get; private set; }
        public BigInteger HeaderValue { get; private set; }
        public byte[] CycleBuffer { get; private set; }
        public byte[] CycleHash { get; private set; }
        public BigInteger CycleValue { get; private set; }
        public Double Difficulty { get; private set; }
        public double BlockDiffAdjusted { get; private set; }
        public byte[] BlockHex { get; private set; }
        public byte[] BlockHash { get; private set; }
        public UInt32[] Cycle { get; private set; }

        public static int CYCLE_LENGTH = 42;

        private UInt32 sipnode(UInt64 k0, UInt64 k1, UInt32 edgeMask, UInt32 nonce, byte uorv) {
            var key = new byte[16];
            BitConverter.GetBytes(k0).CopyTo(key, 0);
            BitConverter.GetBytes(k1).CopyTo(key, 8);

            var node = (UInt32) (SipHash.SipHash24((UInt64)(2 * nonce + uorv), k0, k1) & edgeMask);

            return (node << 1) | uorv;
        }

        private bool checkCycle() {
            var _logger = Log.ForContext<Share>();

            if (Cycle.Length != CYCLE_LENGTH) {
                return false;
            }

            var blakeHash = new HMACBlake2B(256);
            byte[] keys = blakeHash.ComputeHash(Encoding.ASCII.GetBytes(BlockHash.ToHexString()));

            UInt64 k0 = (UInt64) BitConverter.ToInt64(keys.Slice(0, 8), 0).LittleEndian();
            UInt64 k1 = (UInt64) BitConverter.ToInt64(keys.Slice(8, 16), 0).LittleEndian();

            UInt32 edgeMask = (UInt32) (1 << Job.EdgeBits) - 1;

            UInt32[] uvs = new UInt32[2 * CYCLE_LENGTH];
            UInt32 xor0 = 0;
            UInt32 xor1 = 0;

            for (var n = 0; n < CYCLE_LENGTH; n++) {
                if (Cycle[n] > edgeMask) {
                    _logger.Error("POW_TOO_BIG");
                    return false;
                }

                if (n > 0 && Cycle[n] <= Cycle[n - 1]) {
                    _logger.Error("POW_TOO_SMALL");
                    return false;
                }

                xor0 ^= uvs[2 * n] = sipnode(k0, k1, edgeMask, Cycle[n], 0);
                xor1 ^= uvs[2 * n + 1] = sipnode(k0, k1, edgeMask, Cycle[n], 1);
            }

            // matching endpoints imply zero xors
            if ((xor0 | xor1) != 0) {
                var cycleStr = "";
                foreach (var edge in Cycle) {
                    cycleStr += edge.ToString("x") + ",";
                }
                cycleStr = cycleStr.Remove(cycleStr.Length - 1);

                _logger.Debug("Block hash: {0}", BlockHash.ToHexString());
                _logger.Debug("Cycle: {0}", cycleStr);

                _logger.Error("POW_NON_MATCHING");
                return false;
            }

            int m = 0, i = 0, j;
            do { // follow cycle
                for (int k = j = i; (k = (k + 2) % (2 * CYCLE_LENGTH)) != i;) {
                    if (uvs[k] == uvs[i]) { // find other edge endpoint identical to one at i
                        if (j != i) {       // already found one before
                            _logger.Error("POW_BRANCH");
                            return false;
                        }

                        j = k;
                    }
                }

                if (j == i) {
                    _logger.Error("POW_DEAD_END");
                    return false;
                }

                i = j ^ 1;
                m++;
            } while (i != 0); // must cycle back to start or we would have found branch

            if (m != CYCLE_LENGTH) {
                _logger.Error("POW_SHORT_CYCLE");
                return false;
            }

            return true;
        }

        public Share(IStratumMiner miner, UInt64 jobId, IJob job, string extraNonce2, string nTimeString, string nonceString, UInt32[] cycle)
        {
            Miner = miner;
            JobId = jobId;
            Job = job;
            Error = ShareError.None;
            Cycle = cycle;

            var submitTime = TimeHelpers.NowInUnixTimestamp(); // time we recieved the share from miner.

            if (Job == null)
            {
                Error = ShareError.JobNotFound;
                return;
            }

            // check size of miner supplied extraNonce2
            if (extraNonce2.Length/2 != ExtraNonce.ExpectedExtraNonce2Size)
            {
                Error = ShareError.IncorrectExtraNonce2Size;
                return;
            }
            ExtraNonce2 = Convert.ToUInt32(extraNonce2, 16); // set extraNonce2 for the share.

            // check size of miner supplied nTime.
            if (nTimeString.Length != 8)
            {
                Error = ShareError.IncorrectNTimeSize;
                return;
            }
            NTime = Convert.ToUInt32(nTimeString, 16); // read ntime for the share

            // make sure NTime is within range.
            if (NTime < job.BlockTemplate.CurTime || NTime > submitTime + 7200)
            {
                Error = ShareError.NTimeOutOfRange;
                return;
            }

            // check size of miner supplied nonce.
            if (nonceString.Length != 8)
            {
                Error = ShareError.IncorrectNonceSize;
                return;
            }
            Nonce = Convert.ToUInt32(nonceString, 16); // nonce supplied by the miner for the share.

            // set job supplied parameters.
            Height = job.BlockTemplate.Height; // associated job's block height.
            ExtraNonce1 = miner.ExtraNonce; // extra nonce1 assigned to miner.

            // check for duplicate shares.
            if (!Job.RegisterShare(this)) // try to register share with the job and see if it's duplicated or not.
            {
                Error = ShareError.DuplicateShare;
                return;
            }

            // construct the coinbase.
            CoinbaseBuffer = Serializers.SerializeCoinbase(Job, ExtraNonce1, ExtraNonce2);
            CoinbaseHash = Coin.Coinbase.Utils.HashCoinbase(CoinbaseBuffer);

            // create the merkle root.
            MerkleRoot = Job.MerkleTree.WithFirst(CoinbaseHash).ReverseBuffer();

            // create the block headers
            HeaderBuffer = Serializers.SerializeHeader(Job, MerkleRoot, NTime, Nonce);
            HeaderHash = Job.HashAlgorithm.Hash(HeaderBuffer);
            HeaderValue = new BigInteger(HeaderHash);
            BlockHash = HeaderBuffer.DoubleDigest().ReverseBuffer();

            if (!checkCycle()) {
                Error = ShareError.IncorrectCycle;
                return;
            }

            var _logger = Log.ForContext<Share>();

            using (var stream = new MemoryStream())
            {
                stream.WriteByte((byte) Cycle.Length);
                foreach (var edge in Cycle) {
                    stream.WriteValueU32(edge);
                }

                CycleBuffer = stream.ToArray();
            }

            CycleHash = Job.HashAlgorithm.Hash(CycleBuffer);
            CycleValue = new BigInteger(CycleHash);

            // calculate the share difficulty
            Difficulty = ((double)new BigRational(AlgorithmManager.Diff1, CycleValue)) * Job.HashAlgorithm.Multiplier;

            // calculate the block difficulty
            BlockDiffAdjusted = Job.Difficulty * Job.HashAlgorithm.Multiplier;

            // check if block candicate
            if (Job.Target >= CycleValue)
            {
                if (Difficulty < 0) {
                    IsBlockCandidate = false;
                    if (miner.Software == MinerSoftware.MeritMiner && miner.SoftwareVersion == new Version("0.1.0")) {
                        // if we use merit-miner 0.1.0 diff can be negative
                        Error = ShareError.NegativeDifficultyShareOutdatedMiner;
                    } else {
                        Error = ShareError.NegativeDifficultyShare;
                    }
                    return;
                }
                IsBlockCandidate = true;

                BlockHex = Serializers.SerializeBlock(Job, HeaderBuffer, CoinbaseBuffer, CycleBuffer, miner.Pool.Config.Coin.Options.IsProofOfStakeHybrid);
            }
            else
            {
                IsBlockCandidate = false;

                // Check if share difficulty reaches miner difficulty.
                var lowDifficulty = Difficulty / miner.Difficulty < 0.99; // share difficulty should be equal or more then miner's target difficulty.

                if (!lowDifficulty) { // if share difficulty is high enough to match miner's current difficulty.
                    return; // just accept the share.
                }

                if (miner.PreviousDifficulty > 0 && Difficulty >= miner.PreviousDifficulty) {// if the difficulty matches miner's previous difficulty before the last vardiff triggered difficulty change
                    _logger.Debug("\tprevdiff lower; diff >= prevdiff: {0}::{1}", Difficulty, miner.PreviousDifficulty);
                    return; // still accept the share.
                }


                // if the share difficulty can't match miner's current difficulty or previous difficulty
                Error = ShareError.LowDifficultyShare; // then just reject the share with low difficult share error.
            }
        }

        public void SetFoundBlock(Block block, Transaction genTx)
        {
            Block = block;
            GenerationTransaction = genTx;
        }
    }
}
