#r "System.Net.Http"

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text

let simulateBrowserVisit() =
    let c = new HttpClient(BaseAddress = Uri("http://127.0.0.1:8083"))
    c.DefaultRequestHeaders.Accept.Clear()
    c.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.112 Safari/537.36")
   
    let example =
        """{
            "url":"https://bugfree.sharepoint.com/sites/siteCollection/web/SitePages/Start.aspx",
            "loginName":"i:0#.f|membership|rh@bugfree.onmicrosoft.com",
            "pageLoadTime":6896,
            "correlationId": "00000000-0000-0000-0000-000000000000"
        }"""
    let content = new StringContent(example, Encoding.UTF8, "application/json")    
    let result = c.PostAsync("/api/collectOnReady", content).Result
    result.EnsureSuccessStatusCode()