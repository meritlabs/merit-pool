#region License
//
//     MIT License
//
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//     Copyright (C) 2013 - 2017, CoiniumServ Project
//     Hüseyin Uslu, shalafiraistlin at gmail dot com
//     https://github.com/bonesoul/CoiniumServ
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


            UInt32 e1 = ExtraNonce1.BigEndian();
            UInt32 e2 = ExtraNonce2;

            // construct the coinbase.
            CoinbaseBuffer = Serializers.SerializeCoinbase(Job, ExtraNonce1, ExtraNonce2);

            // remove withness data - 32 bytes in the end of tx followed by 4 bytes of locktime
            var coinbaseNoWitness = new byte[CoinbaseBuffer.Length - 32];
            Array.Copy(CoinbaseBuffer, 0, coinbaseNoWitness, 0, CoinbaseBuffer.Length - 36);
            Array.Copy(CoinbaseBuffer, CoinbaseBuffer.Length - 4, coinbaseNoWitness, coinbaseNoWitness.Length - 4, 4);

            // CoinbaseHash = Coin.Coinbase.Utils.HashCoinbase(coinbaseNoWitness);
            CoinbaseHash = Coin.Coinbase.Utils.HashCoinbase(CoinbaseBuffer);


            var _logger = Log.ForContext<Share>();

            _logger.Information("Share height: {0}", Height);
            _logger.Information("ExtraNonce1: {0}", BitConverter.GetBytes(e1).ToHexString());
            _logger.Information("ExtraNonce2: {0}", BitConverter.GetBytes(e2.BigEndian()).ToHexString());
            _logger.Information("Script final: {0}", Job.GenerationTransaction.CoinbaseSignatureScript.Final.ToHexString());

            _logger.Information("CoinbaseHash: {0}", CoinbaseHash.ToString());
            _logger.Information("CoinbaseBuffer: {0}", CoinbaseBuffer.ToHexString());
            _logger.Information("coinbaseNoWitness: {0}", coinbaseNoWitness.ToHexString());

            // create the merkle root.
            MerkleRoot = Job.MerkleTree.WithFirst(CoinbaseHash).ReverseBuffer();

            // create the block headers
            HeaderBuffer = Serializers.SerializeHeader(Job, MerkleRoot, NTime, Nonce, Cycle);
            HeaderHash = Job.HashAlgorithm.Hash(HeaderBuffer);
            HeaderValue = new BigInteger(HeaderHash);
            BlockHash = HeaderBuffer.DoubleDigest().ReverseBuffer();

            _logger.Information("Header: {0}", HeaderBuffer.ToHexString());

            using (var stream = new MemoryStream())
            {
                stream.WriteByte((byte) Cycle.Length);
                foreach (var edge in Cycle) {
                    stream.WriteValueU32(edge);
                }

                CycleBuffer = stream.ToArray();
            }

            CycleHash = Job.HashAlgorithm.Hash(CycleBuffer).ReverseBuffer();
            CycleValue = new BigInteger(CycleHash);
            _logger.Information("CycleHash: {0}", CycleHash.ToHexString());
            _logger.Information("BlockHash: {0}", BlockHash.ToHexString());
            _logger.Information("MerkleRoot: {0}", MerkleRoot.ToHexString());

            // calculate the share difficulty
            Difficulty = ((double)new BigRational(AlgorithmManager.Diff1, CycleValue)) * Job.HashAlgorithm.Multiplier;

            // calculate the block difficulty
            BlockDiffAdjusted = Job.Difficulty * Job.HashAlgorithm.Multiplier;

            _logger.Information("N transactions: {0}", Job.BlockTemplate.Transactions.Length);
            _logger.Information("N invites: {0}", Job.BlockTemplate.Invites.Length);
            _logger.Information("N referrals: {0}", Job.BlockTemplate.Referrals.Length);

// 0000002844a316ba5e464e5939577881ab5525ed88daff81880441dee4614aa02d49cb7c73b086e017b91c0228366c00dd19d856db6b958c503d43b088e8a466027b0e25c369f85a62b63620ccccccd81701020000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff1d03106f0103d5491708e0000002000000000b2f4d65726974506f6f6c2fffffffff0707e3a74d000000001976a9149584ceec446d86fbc088f9d952bb89256eabd6d188acc0816b02000000001976a91478aa81231ee49018222a46d61d778d9f725ad85488ac8063850f000000001976a91432274b95b49218c294933a7323de9ec7e6703f2188acc0816b02000000001976a91478aa81231ee49018222a46d61d778d9f725ad85488acc079c50f000000001976a914658e71c175a7dac7fca792b0a336b6fb53095cdc88acc08c6c05000000001976a914920f7476ffbf1b0cf771bdffe6905f37db49b7c588ac0000000000000000266a24aa21a9ed76e72e643f7a78045b2039bc137331b33ea0a48dfe4a7d6f5a43b03c2583256e0120000000000000000000000000000000000000000000000000000000000000000000000000
// 0000002844a316ba5e464e5939577881ab5525ed88daff81880441dee4614aa02d49cb7c73b086e017b91c0228366c00dd19d856db6b958c503d43b088e8a466027b0e25c369f85a62b63620ccccccd81701020000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff1d03106f0103d5491708e0000002000000000b2f4d65726974506f6f6c2fffffffff0707e3a74d000000001976a9149584ceec446d86fbc088f9d952bb89256eabd6d188acc0816b02000000001976a91478aa81231ee49018222a46d61d778d9f725ad85488ac8063850f000000001976a91432274b95b49218c294933a7323de9ec7e6703f2188acc0816b02000000001976a91478aa81231ee49018222a46d61d778d9f725ad85488acc079c50f000000001976a914658e71c175a7dac7fca792b0a336b6fb53095cdc88acc08c6c05000000001976a914920f7476ffbf1b0cf771bdffe6905f37db49b7c588ac0000000000000000266a24aa21a9ed76e72e643f7a78045b2039bc137331b33ea0a48dfe4a7d6f5a43b03c2583256e012000000000000000000000000000000000000000000000000000000000000000000000000002000000070051e9aaebdafa38c3bc932be501ae7589c2828720a8f1576245a90fa41032bf00000000484730440220443e903c22bd77b23d1b52ab381cd7f420d889701c040f7d69a1fdbd198505fd022002ead5e61678a1e2fa749c52b7570dd241e467c83e81532bd44bb58e8dcce76401feffffff03e80a089b42c404ea6207451643628d35402886967a41e7dcc3ff73f3d1d8cb000000006a47304402203c1c1a17eabfb24fedc00c146a79369690b3753e12ac026df74ecca7d33c14df022037314316dfc38d062cb5aa5b5354adc70843cc4bc7c4ec8d685cb15fc33c43a4012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffff29341247c8dcf67b277a957999764bd8daa6fb38e538056c05e17493cc1e3bf8010000006a473044022069e7e9cf69faf562d6062c5d7bc790c368eed6448ca05b3cb4e2f035ac79c4880220515e50ac3141af04658d48206bbebac3682b5c8a7381af5bb46a54575c1bdbde012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffff3872902632b9149f4d8816da21ed32247e85835e762528d5d841f6f105d89499000000006a473044022008bfbf5d99eab9c728c031f98608ca0446e79faac4146ec7ff2e4f451a48e4c8022051b1acf9d815ec2fb5adb3c1fbc5a02c3ea5f3cdcacbd9505aea0e8b8974246b012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffffc02c4101a95d8019b17648156b81dfd2fb7db577a96ec8c1fb88358640841725010000006b483045022100ffdbdd8560ab93e6bacf4106a1da88d48cd18ece8651d6792625bf221478478d0220726471dbfef3f9205b19a8c22ad34f8dcba1d616c7dee22b3b2f2a2a1aba1a56012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffffe810b959cbf6f3fa97ae424abf0e543b8355c930da4a426da9531dfc2f06623f000000006a4730440220790a8ac4ae70ccb569632f8fa9c4d891452f456b235915bbb9b7ba456acd28c0022032feb61c3b728bcf33a1e2927548fcc771ec79dbe7887b625000cb432b4912fe012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffffff1d148fb24dd2ae4f38d55271fce9755e22d349e49cef36dfadce51c0b972fb000000006a47304402206e1b6a5568493ad4ef8ee28768daef95b25503b6b6fac2932ad3ae89178f252b0220031d6cb10e16eb897ef6bc32bf383264964251b873fcf2b2ea017962bd96c3b001210357a183abe66c31951370485fb34d5d3b57dca1a3b921533f4618ce6d6d463a46feffffff02c5f80e00000000001976a914f971453d61bc8117ae16918f221eea91f24b2af988acd81a959a000000001976a914cd724d3b246fac1dd9ee19590bec99d16ec3192888ac0f6f0100020000000600a963206fe04fe5ed4628d766f3c35c70461ff5642921bbd2135a4321ce3220010000006b483045022100cad1806798f0c24b41f6ff5377dd2282df7cd5a894baa7da36f6c112e21be1bc02206a630f6e5dbe5a1adb04c60d5c534864a4525470b5897ff4e82747dc2468b0e9012103c2bdf3f923a149a56050c24a96367b581a14d8009df136d3827e012b52e5c3edfeffffff0842b2cda0118e1a378a723b9eb7ba53633be1d148e73f5e6d7402f6435a93b30000000049483045022100dd8b1999d8be1b83a72ded62a4386ad5b589d05d9dc1736f70e6b0341d866d1f02205ef2d00744e88329c4b3b76eb8d15c99fe040d163a8ea5558bb8cc7a7167024d01feffffff3666b35095525f511a71732b68fe40cf70433d25bbee4ec3d70ef356f324d7af030000006b483045022100b6249919e104908a13ed6f4c47546e7cc8661f0e549b35ba0f40b96493d1d4b5022053954be1d682fedc0b96a03f068cefdc02dd58cea6abdd4d4f82ca78c7d1c36a012103c2bdf3f923a149a56050c24a96367b581a14d8009df136d3827e012b52e5c3edfeffffff8a869411a988b4643efd14106e5a3357055cca7b56e47a8c21a4161a86e44b41040000006a47304402202221c4b5af52de4f1d33e922cd0d6d72d170b5dce949171cdf69c5fd3d04466802202afcb6958a4c74f497d0e097626e8a06bfe3d7b62dabfe5bbd315b98643db341012103d64f0f44aea4454d942300a1b33362533bc025534611e0aed548e96a90013e28feffffffc7a48289ffd0b14cd0e95a6eb874432ec6ab560c5d16bbe0ae24b95382c5ef24020000006b483045022100cf7336f1230fe1139cd5e43340b2ebbc4f500b607caa710906d39f73a8a8e320022034146e4c9861202c19d05fe44fbfaa62894ccb062a5178def368ef194d825234012103c2bdf3f923a149a56050c24a96367b581a14d8009df136d3827e012b52e5c3edfefffffff68243496ff1957716d019a1c98f3d99d0d443b80ea746f9dcab70fa0c7bd6520000000049483045022100d9c96d6ce19b8d11826eabd7e6ae6d5203c352b52ada736c0b3cf1b9467f83cf0220395734809529681654c0d22e4c5f261b03c6e8e0b9b3092b38dda8b154e48c8901feffffff02d0e24db3000000001976a9141f307e9346cc67c5c8e61fe3e4dc4a870296751288acf91a1300000000001976a914f971453d61bc8117ae16918f221eea91f24b2af988ac0e6f01000103000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0503106f0100ffffffff0101000000000000001976a914883b11b93f1c568bc0d14a44b61cdd40f9a63ac788ac0000000000
// 000000a888ef00ce9741c939696bc050e026fd52238960ac98d218f549123ac1f42c27c96aeaa817aa686af700778ccab4825539f1814b258e2da8deb0b0a771f6f57a148969f85a62b636205b000000172a596b0500bd3b0b0011c70c009f940d003f9e0d004ae012000e031700c25419001f041e00b73a1e0011f31e00f8032100b65f2300f66f2800b72a2a0001c22b0048082f0098843100fafe3400281a360070133a00d66e3a00af803a0038063f00986b4a00a12a4c0088954d004ea04f00d6b756001545570001e357006d5d590079cd6100256a620004d964001b6d6700bca86b0022e36e00b4fa710048ee7500fabd7b00411e7f0001020000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff06030f6f010102ffffffff0720789c4d000000002321023f5bb2a67822fb6bfbad1a0604fb9ce7d497b869fa8f12ea76c043b4f43c3c02acc02bc611000000001976a9144b3a82a24f1deb28e53245bcb1225afcfdc452ae88aca003f601000000001976a914f971453d61bc8117ae16918f221eea91f24b2af988ace0e1380a000000001976a91423e7e76db478e1998483a4a1c448d48d1ac6640088acc0286b01000000001976a91478aa81231ee49018222a46d61d778d9f725ad85488ace0e1380a000000001976a91423e7e76db478e1998483a4a1c448d48d1ac6640088ac0000000000000000266a24aa21a9ede2f61c3f71d1defd3fa999dfa36953755c690689799962b48bebd836974e8cf901200000000000000000000000000000000000000000000000000000000000000000000000000000

// 020000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff1d0365700103e849170818000003000000000b2f4d65726974506f6f6c2fffffffff0796619d4d000000001976a9149584ceec446d86fbc088f9d952bb89256eabd6d188ac808c2e0b000000001976a91423e7e76db478e1998483a4a1c448d48d1ac6640088ac4060ec04000000001976a9147973e37e76bf9e8a6f58c19af866d291c577564988ac808c2e0b000000001976a91423e7e76db478e1998483a4a1c448d48d1ac6640088ace06ee309000000001976a91432274b95b49218c294933a7323de9ec7e6703f2188acc0336c04000000001976a914bdd1e666891ae5733074474da8eca909b5eab56288ac0000000000000000266a24aa21a9ed87159fe5be9f38600361bd7464a212b26111e174e39248e8bdcfa759ce3fdd0e0120000000000000000000000000000000000000000000000000000000000000000000000000
            // check if block candicate
            if (Job.Target >= CycleValue)
            {
                IsBlockCandidate = true;
                BlockHex = Serializers.SerializeBlock(Job, HeaderBuffer, CoinbaseBuffer, CycleBuffer, miner.Pool.Config.Coin.Options.IsProofOfStakeHybrid);
            }
            else
            {
                IsBlockCandidate = false;

                // Check if share difficulty reaches miner difficulty.
                var lowDifficulty = Difficulty/miner.Difficulty < 0.99; // share difficulty should be equal or more then miner's target difficulty.

                if (!lowDifficulty) // if share difficulty is high enough to match miner's current difficulty.
                    return; // just accept the share.

                if (Difficulty >= miner.PreviousDifficulty) // if the difficulty matches miner's previous difficulty before the last vardiff triggered difficulty change
                    return; // still accept the share.

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
