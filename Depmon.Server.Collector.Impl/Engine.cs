﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Dapper;
using Depmon.Server.Collector.Configuration;
using Depmon.Server.Database;
using Depmon.Server.Database.Queries;
using Depmon.Server.Domain;
using Depmon.Server.Services;

namespace Depmon.Server.Collector.Impl
{
    public class Engine : IEngine
    {
        private readonly CancellationTokenSource _cancellationSource;
        private Task[] _tasks;
        private IObjectFactory _objectFactory;
        private Timer _timer;

        public Engine(IObjectFactory objectFactory)
        {
            _cancellationSource = new CancellationTokenSource();
            _objectFactory = objectFactory;
        }

        public void Start(Settings config)
        {
            Console.WriteLine("Monitoring starting...");
            
            EveryDayNotification(config.Notification);

            _tasks = new Task[config.Mailboxes.Count];

            for (var i = 0; i < config.Mailboxes.Count; i++)
            {
                Thread.Sleep(config.Iteration.Delay);

                var mailbox = config.Mailboxes[i];

                _tasks[i] = Task.Run(() => OnProcess(mailbox, _cancellationSource.Token), _cancellationSource.Token);
            }
        }

        private void EveryDayNotification(Notification settings)
        {
            var time = settings.EveryDay.Time;

            DateTime currentTime = DateTime.Now;
            DateTime scheduleTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, time.Hours, time.Minutes, time.Seconds);

            TimeSpan period = TimeSpan.FromDays(1); 
                                                        
            TimeSpan initialInterval;
            if (currentTime <= scheduleTime)
            {
                initialInterval = scheduleTime.Subtract(currentTime);
            }
            else
            {
                initialInterval = scheduleTime.AddDays(1).Subtract(currentTime);
            }

            _timer = new Timer(SendEveryDayNotification, null, initialInterval, period);

        }

        private void SendEveryDayNotification(object state)
        {
            using (var scope = _objectFactory.CreateScope())
            {
                var notificationService = scope.Resolve<INotificationService>();

                notificationService.EveryDay();
            }
        }

        private void SendNewReportNotification(Report report, Fact[] facts)
        {
            if (!NeedToSendNotification(report, facts))
            {
                return;
            }
            
            using (var scope = _objectFactory.CreateScope())
            {
                var notificationService = scope.Resolve<INotificationService>();

                notificationService.SendNewReport(report, facts);
            }
        }

        private bool NeedToSendNotification(Report report, Fact[] facts)
        {
            using (var scope = _objectFactory.CreateScope())
            {
                var unitOfWork = scope.Resolve<IUnitOfWork>();
                var previousReportDateSql = QueryStore.PreviousReportBySourceCode();

                var previousReportDate = unitOfWork.Session.Query(previousReportDateSql, new {report.SourceCode});

                var currentCount = Enum.GetValues(typeof (FactLevel))
                    .Cast<FactLevel>()
                    .ToDictionary(l => (int)l, l => facts.Count(f => f.Level == l));

                var previousCount = Enum.GetValues(typeof (FactLevel))
                    .Cast<FactLevel>()
                    .ToDictionary(l => (int) l, l => previousReportDate.Count(f => f.Level == (int) l));

                foreach (var current in currentCount)
                {
                    if (previousCount[current.Key] != current.Value)
                    {
                        return true;
                    } 
                }

                return false;
            }
        }

        public void Stop()
        {
            Console.WriteLine("Monitoring breaking...");

            _cancellationSource.Cancel();
            Task.WaitAll(_tasks);
        }

        private void OnProcess(Mailbox mailbox, CancellationToken cancellationToken)
        {
            Console.WriteLine("[{1}] - [{0}] monitoring started", mailbox.Name, DateTime.Now);

            while (!cancellationToken.IsCancellationRequested)
            {
                using (var scope = _objectFactory.CreateScope())
                {
                    IMailReciever mailReciever = scope.Resolve<IMailReciever>();
                    IReportRegistry reportRegistry = scope.Resolve<IReportRegistry>();
                    ICsvReader csvReader = scope.Resolve<ICsvReader>();
                    IList<Stream> data = null;

                    try
                    {
                        data = mailReciever.Load(mailbox);

                        foreach (var stream in data)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var messageBase64 = reader.ReadToEnd();
                                var message = Encoding.UTF8.GetString(Convert.FromBase64String(messageBase64));

                                var dtos = csvReader.Read(message);

                                if (!dtos.Any()) continue;
                                var report = reportRegistry.Save(dtos);

                                SendNewReportNotification(report, dtos);
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("[{1}] iteration failed: {0}", e.Message, mailbox.Name);
                    }
                    finally
                    {
                        Dispose(data);
                    }
                }
                cancellationToken.WaitHandle.WaitOne(mailbox.Delay);
            }
        }

        private void Dispose(IList<Stream> data)
        {
            if (data == null)
            {
                return;
            }

            foreach (var stream in data)
            {
                stream.Close();
                stream.Dispose();
            }

            data.Clear();
        }
    }
}
