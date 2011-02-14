﻿using System;
using System.Threading.Tasks;

namespace BTDB.ODBLayer
{
    public interface IMidLevelDB : IDisposable 
    {
        void Open(ILowLevelDB lowLevelDB, bool dispose);

        IMidLevelDBTransaction StartTransaction();

        Task<IMidLevelDBTransaction> StartWritingTransaction();
    }
}
