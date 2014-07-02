﻿#region License
// 
//     CoiniumServ - Crypto Currency Mining Pool Server Software
//     Copyright (C) 2013 - 2014, CoiniumServ Project - http://www.coinium.org
//     https://github.com/CoiniumServ/CoiniumServ
// 
//     This software is dual-licensed: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//    
//     For the terms of this license, see licenses/gpl_v3.txt.
// 
//     Alternatively, you can license this software under a commercial
//     license or white-label it as set out in licenses/commercial.txt.
// 
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using Coinium.Daemon;
using Coinium.Mining.Pools;
using Coinium.Net.Server.Sockets;
using Serilog;

namespace Coinium.Mining.Miners
{
    public class MinerManager : IMinerManager
    {
        private readonly Dictionary<Int32, IMiner> _miners = new Dictionary<Int32, IMiner>();
        private int _counter = 0; // counter for assigining unique id's to miners.

        private readonly IDaemonClient _daemonClient;

        public event EventHandler MinerAuthenticated;

        public MinerManager(IDaemonClient daemonClient)
        {
            _daemonClient = daemonClient;
        }

        public IList<IMiner> GetAll()
        {
            return _miners.Values.ToList();
        }

        public IMiner GetMiner(Int32 id)
        {
            return _miners.ContainsKey(id) ? _miners[id] : null;
        }

        public IMiner GetByConnection(IConnection connection)
        {
            return (from pair in _miners  // returned the miner associated with the given connection.
                let client = (IClient) pair.Value 
                where client.Connection == connection 
                select pair.Value).FirstOrDefault();
        }

        public T Create<T>(IPool pool) where T : IMiner
        {
            var instance = Activator.CreateInstance(typeof(T), new object[] { _counter++, pool, this }); // create an instance of the miner.
            var miner = (IMiner)instance;
            _miners.Add(miner.Id, miner); // add it to our collection.

            return (T)miner;
        }

        public T Create<T>(UInt32 extraNonce, IConnection connection, IPool pool) where T : IMiner
        {
            var instance = Activator.CreateInstance(typeof(T), new object[] { _counter++, extraNonce, connection, pool, this });  // create an instance of the miner.
            var miner = (IMiner)instance;
            _miners.Add(miner.Id, miner); // add it to our collection.           

            return (T)miner;
        }

        public void Remove(IConnection connection)
        {
            var miner = (from pair in _miners // find the miner associated with the connection.
                let client = (IClient)pair.Value 
                where client.Connection == connection 
                select pair.Value).FirstOrDefault();

            if (miner != null)
                _miners.Remove(miner.Id);
        }

        public void Authenticate(IMiner miner)
        {
            miner.Authenticated = _daemonClient.ValidateAddress(miner.Username).IsValid;

            Log.ForContext<MinerManager>().Information(miner.Authenticated ? "Authenticated miner: {0} [{1}]" : "Unauthenticated miner: {0} [{1}]",
                miner.Username, ((IClient) miner).Connection.RemoteEndPoint);

            if(miner.Authenticated) // if miner authenticated successfully.
                OnMinerAuthenticated(new MinerEventArgs(miner)); // notify listeners about the new authenticated miner.
        }

        protected virtual void OnMinerAuthenticated(MinerEventArgs e)
        {
            var handler = MinerAuthenticated;

            if (handler != null)
                handler(this, e);
        }
    }
}
