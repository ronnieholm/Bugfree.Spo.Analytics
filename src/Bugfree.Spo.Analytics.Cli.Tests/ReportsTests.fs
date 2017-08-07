module Bugfree.Spo.Analytics.Cli.Tests.ReportsTests

#if INTERACTIVE
#r "System.Net.Http"
#endif

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text

let xxx () =
    let c = new HttpClient(BaseAddress = Uri("http://127.0.0.1:8083"))
    c.DefaultRequestHeaders.Accept.Clear()
    c.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.112 Safari/537.36") 
    let result = c.GetAsync("/api/collectOnReady/internalExternalIPOriginVisitsInDateRange?from=2017-03-30 22:00&to=2017-04-01 22:00").Result
    result.EnsureSuccessStatusCode()
 
