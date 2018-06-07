#region License
//
//     MIT License
//
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//
//     Copyright (C) 2013 - 2017, CoiniumServ Project
//     Copyright (C) 2017-2018 The Merit Foundation developers
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

using System.Runtime.CompilerServices;

namespace CoiniumServ.Algorithms
{
    public static class SipHash
    {
        public static ulong SipHash24ForcedInline(ulong nonce, ulong k0, ulong k1)
        {
            var v0 = 0x736f6d6570736575 ^ k0;
            var v1 = 0x646f72616e646f6d ^ k1;
            var v2 = 0x6c7967656e657261 ^ k0;
            var v3 = 0x7465646279746573 ^ k1 ^ nonce;

            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));
            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));

            v0 ^= nonce;
            v2 ^= 0xff;

            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));
            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));
            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));
            v0 += v1;
            v1 = (v1 << 13) | (v1 >> (64 - 13));
            v1 ^= v0;
            v0 = (v0 << 32) | (v0 >> (64 - 32));

            v2 += v3;
            v3 = (v3 << 16) | (v3 >> (64 - 16));
            v3 ^= v2;

            v0 += v3;
            v3 = (v3 << 21) | (v3 >> (64 - 21));
            v3 ^= v0;

            v2 += v1;
            v1 = (v1 << 17) | (v1 >> (64 - 17));
            v1 ^= v2;
            v2 = (v2 << 32) | (v2 >> (64 - 32));

            return v0 ^ v1 ^ v2 ^ v3;
        }

        // Included only to show that casting to long isn't really much faster than using
        // the bit-shift function that is inlined.
        public static ulong SipHash24(ulong nonce, ulong k0, ulong k1)
        {
            var v0 = 0x736f6d6570736575 ^ k0;
            var v1 = 0x646f72616e646f6d ^ k1;
            var v2 = 0x6c7967656e657261 ^ k0;
            var v3 = 0x7465646279746573 ^ k1 ^ nonce;

            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            v0 ^= nonce;
            v2 ^= 0xff;

            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);

            return (v0 ^ v1) ^ (v2 ^ v3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SipRound(ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3)
        {
            v0 += v1;
            v1 = Rotl(v1, 13);
            v1 ^= v0;
            v0 = Rotl(v0, 32);

            v2 += v3;
            v3 = Rotl(v3, 16);
            v3 ^= v2;

            v0 += v3;
            v3 = Rotl(v3, 21);
            v3 ^= v0;

            v2 += v1;
            v1 = Rotl(v1, 17);
            v1 ^= v2;
            v2 = Rotl(v2, 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Rotl(ulong x, int b)
        {
            return (x << b) | (x >> (64 - b));
        }
    }
}
