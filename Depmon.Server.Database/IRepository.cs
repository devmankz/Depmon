﻿using System;
using System.Collections.Generic;

namespace Depmon.Server.Database
{
    public interface IRepository<T> : IDisposable
    {
        IEnumerable<T> GetAll();

        T GetById(int id);

        void InsertMany(params T[] entities);

        int Save(T entity);

        void Delete(int id);
    }
}
