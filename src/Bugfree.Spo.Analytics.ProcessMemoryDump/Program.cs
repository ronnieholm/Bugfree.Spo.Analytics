using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Microsoft.FSharp.Core;
using ClrMD.Extensions;
using static Bugfree.Spo.Analytics.Cli.Domain;
using static Bugfree.Spo.Analytics.Cli.Agents;

namespace Bugfree.Spo.Analytics.MemoryDumpProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            var visits = new List<Visit>();
            using (ClrMDSession session = ClrMDSession.LoadCrashDump(@"C:\AzureDump\Bugfree.Spo.Analytics.Cli-d3c510-08-01-18-51-09.dmp"))
            {
                foreach (ClrObject o in session.EnumerateClrObjects("Bugfree.Spo.Analytics.Cli.Domain+Visit"))
                {
                    var pageLoadTime = (int?)o["PageLoadTime@"]["value"].SimpleValue ?? null;
                    var userAgent = (string)o["UserAgent@"]["value"].SimpleValue ?? null;
                    var v = new Visit(
                        (Guid)o["CorrelationId@"],
                        (DateTime)o["Timestamp@"],
                        (string)o["LoginName@"],
                        (string)o["SiteCollectionUrl@"],
                        (string)o["VisitUrl@"], 
                        pageLoadTime == null ? FSharpOption<int>.None : new FSharpOption<int>(pageLoadTime.Value),
                        (IPAddress)o["IP@"],
                        userAgent == null ? FSharpOption<string>.None : new FSharpOption<string>(userAgent));
                    visits.Add(v);
                }

                // Enumerating the heap doesn't preserve allocation order. Hence we impose an
                // order using the visit's timestamp. This allows for each visit inspection.
                foreach (var v in visits.OrderBy(v => v.Timestamp))
                {
                    visitor.Post(VisitorMessage.NewVisit(v));
                }

                // Visitor mailbox processor processes messages/visits on a separate thread. 
                // We must wait for the thread to finish processing before terminating the 
                // program.
                while (true)
                {
                    var l = visitor.CurrentQueueLength;
                    Console.WriteLine($"Queue length: {l}");
                    Thread.Sleep(5000);

                    if (l == 0)
                    {
                        break;
                    }
                }

                Console.ReadKey();
            }
        }
    }
}