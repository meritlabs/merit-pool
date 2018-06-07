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
using StackExchange.Redis;
using System.Collections.Generic;

namespace CoiniumServ.Persistance.Providers.Redis
{

    /// <summary>
    /// Represents a client connection to a Redis server instance
    /// </summary>
    public class RedisClient
    {
        /// <summary>
        /// Get a value indicating whether the Redis client is connected to the server
        /// </summary>
        public bool IsConnected => ConnectionMultiplexer.IsConnected;

        public ConnectionMultiplexer ConnectionMultiplexer { get; }

        public IDatabase Database { get; set; }

        /// <summary>
        /// Create a new RedisClient
        /// </summary>
        /// <param name="host">Redis server hostname</param>
        /// <param name="port">Redis server port</param>
        /// <param name="password"></param>
        /// <param name="extraOptions"></param>
        public RedisClient(string host, int port, string password = "", string extraOptions = "")
        {
            var portString = port == 0 ? "" : $":{port}";
            var passwordString = string.IsNullOrEmpty(password) ? "" : $",password={password}";
            var extraOptionsString = string.IsNullOrEmpty(extraOptions) ? "" : $",{extraOptions}";
            var connectionString = $"{host}{portString}{passwordString}{extraOptionsString}";

            // create the connection
            ConnectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
        }


        internal void Select(int databaseId)
        {
            Database = ConnectionMultiplexer.GetDatabase(databaseId);
        }

        internal void HIncrByFloat(string key, string field, double increment)
        {
            Database.HashIncrement(key, field, increment);
        }

        internal void HIncrBy(string key, string field, int increment)
        {
            Database.HashIncrement(key, field, increment);
        }

        internal void Del(string key)
        {
            Database.KeyDelete(key);
        }

        internal Dictionary<string, string> HGetAll(string key)
        {
            return Database.HashGetAll(key).ToStringDictionary();
        }

        internal void ZAdd(string key, params Tuple<double, string>[] memberScores)
        {
            var entries = new SortedSetEntry[memberScores.Length];

            for (var i = 0; i < memberScores.Length; ++i)
            {
                entries[i] = new SortedSetEntry(memberScores[i].Item2, memberScores[i].Item1);

            }

            Database.SortedSetAdd(key, entries);
        }

        internal void Rename(string key, string newKey)
        {
            Database.KeyRename(key, newKey);
        }

        internal void ZRemRangeByScore(string key, double min, double max)
        {
            Database.SortedSetRemoveRangeByScore(key, min, max);
        }

        internal string[] ZRangeByScore(string key, double min, double max)
        {
            return Database.SortedSetRangeByScore(key, min, max).ToStringArray();
        }
    }
}