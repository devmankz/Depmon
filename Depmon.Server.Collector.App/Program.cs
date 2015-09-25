﻿using System;
using Autofac;
using Depmon.Server.Collector.Configuration;
using Depmon.Server.Collector.Impl;

namespace Depmon.Server.Collector.App
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Deployment Health Monitor [Version 0.1]");
            Console.WriteLine("(c) Sergey Rubtsov, Dastan Uskembayev, 2015.");
            Console.WriteLine("\nPress 'X' for break...\n\n");

            IContainer container = new AutofacContainer().GetContainer();
            IConfigReader cr;
            IEngine engine;

            using (var scope = container.BeginLifetimeScope())
            {
                cr = scope.Resolve<IConfigReader>();
                engine = scope.Resolve<IEngine>();
            }

            var config = cr.Read();
            engine.Start(config);

            var finish = false;
            do
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.X)
                {
                    finish = true;
                }
            } while (!finish);

            engine.Stop();
        }
    }
}
