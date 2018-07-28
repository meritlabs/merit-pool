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

namespace CoiniumServ.Persistance.Providers.Redis
{
    public interface IRedisProviderConfig:IStorageProviderConfig
    {
        /// <summary>
        /// redis host.
        /// </summary>
        string Host { get; }

        /// <summary>
        /// redis port.
        /// </summary>
        Int32 Port { get; }

        /// <summary>
        /// the password if redis installation requires one.
        /// </summary>
        string Password { get; }

        /// <summary>
        /// redis database instance id
        /// </summary>
        int DatabaseId { get; }

        /// <summary>
        /// ResponseTimeout is the number of ms before we treat the connection as dead - 
        /// since it's been that long since we've seen activity on the connection and treat it as "stale"
        /// https://github.com/StackExchange/StackExchange.Redis/issues/235
        /// </summary>
        int ResponseTimeout { get; }

        /// <summary>
        /// SyncTimeout is the amount of time to wait on a specific operation to complete
        /// https://github.com/StackExchange/StackExchange.Redis/issues/235
        /// </summary>
        int SyncTimeout { get; }
    }
}
