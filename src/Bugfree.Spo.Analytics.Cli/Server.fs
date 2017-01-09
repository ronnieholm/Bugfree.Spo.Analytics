module Bugfree.Spo.Analytics.Cli.Server

open System
open System.IO
open System.Net
open System.Text
open FSharp.Data
open Suave
open Suave.Filters
open Suave.Files
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open Suave.Logging
open Suave.Writers
open Agents
open Reports

let [<Literal>] postOnReadyExample =
    """{
        "correlationId": "25D7D3D3-2C4F-48DD-8223-35F5A9E4238E",
        "url":"https://bugfree.sharepoint.com/sites/siteCollection/web/sitePages/start.aspx",
        "loginName":"i:0#.f|membership|rh@bugfree.onmicrosoft.com"
    }"""

let [<Literal>] postOnLoadExample =
    """{
        "correlationId": "25D7D3D3-2C4F-48DD-8223-35F5A9E4238E",
        "url":"https://bugfree.sharepoint.com/sites/siteCollection/web/sitePages/start.aspx",
        "loginName":"i:0#.f|membership|rh@bugfree.onmicrosoft.com",
        "pageLoadTime":1234
    }"""

type PostOnReadyJson = JsonProvider<postOnReadyExample>
type PostOnLoadJson = JsonProvider<postOnLoadExample>

let getUserAgent (request: HttpRequest) =
    match (request.header "User-Agent") with
    | Choice1Of2 ua -> Some ua
    | Choice2Of2 _ -> None

let callOriginatesFromSharePoint (url: string) =
    // In case a user saves a page containing the JavaScript callback to an MHT file, 
    // opening the saved page for offline viewing triggers calls to our endpoints. In
    // that case, the page URL starts with file://. We filter our such requests.
    url.StartsWith("https://")

let getXForwardedFor (request: HttpRequest) =
    // Using the HttpPlatformHandler and running in Azure, Azure's load balancer/ 
    // reverse proxy adds the x-forwarded-for header to the request containing the 
    // original client IP and port.
    match (request.header "x-forwarded-for") with
    | Choice1Of2 xff -> 
        // Not sure why the same IP is listed multiple times so we assert the IPs 
        // (without port number) are the same. That way we can use any IP for 
        // further processing. The x-forwarded-for contains a string of this format:
        // let xff = "12.345.678.90:57505, 12.345.678.90, 12.345.678.90:0"
        let ips = 
            xff.Split(',')
            |> Array.map (fun s -> s.Trim())
            |> Array.map (fun s ->               
                let lastColon = s.LastIndexOf(':')
                if lastColon <> -1 then s.Substring(0, lastColon) else s)
            |> Array.distinct 

        if ips |> Array.length <> 1 then failwithf "x-forwarded-for assumed to contain one unique IP address: %s" xff
        Some (IPAddress.Parse(ips.[0]))
    | Choice2Of2 _ -> None

let postOnReady (request: HttpRequest) =
    let json = PostOnReadyJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    if callOriginatesFromSharePoint json.Url then
        Agents.visitor.Post (Visit {
            CorrelationId = json.CorrelationId
            Timestamp = DateTime.UtcNow
            LoginName = json.LoginName
            Url = json.Url
            PageLoadTime = None
            IP = match getXForwardedFor request with Some ip -> ip | None -> failwith "Expected IP address"
            UserAgent = getUserAgent request })
        OK "processedOnReady"
    else
        OK "callNotOriginatesFromSharePoint"

let postOnLoad (request: HttpRequest) =
    let json = PostOnLoadJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    if callOriginatesFromSharePoint json.Url then
        Agents.visitor.Post (Visit {
            CorrelationId = json.CorrelationId
            Timestamp = DateTime.UtcNow
            LoginName = json.LoginName
            Url = json.Url
            PageLoadTime = Some json.PageLoadTime
            IP = match getXForwardedFor request with Some ip -> ip | None -> failwith "Expected IP address"
            UserAgent = getUserAgent request })
        OK "processedOnLoad"
    else
        OK "callNotOriginatesFromSharePoint"

let serializeToJson o =
    let jsonSerializerSettings = Newtonsoft.Json.JsonSerializerSettings()
    jsonSerializerSettings.ContractResolver <- new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
    Newtonsoft.Json.JsonConvert.SerializeObject(o, jsonSerializerSettings)    

let getVisitorByVisitorCountInDateRange (request: HttpRequest) =
    match request.queryParam "from" with
    | Choice1Of2 t -> 
        match request.queryParam "to" with
        | Choice1Of2 u -> 
            Reports.generateVisitorsByVisitorCount (DateTime.Parse(t)) (DateTime.Parse(u))
            |> serializeToJson
            |> OK >=> Writers.setMimeType "application/json; charset=utf-8"
        | Choice2Of2 _ -> BAD_REQUEST "Missing to"
    | Choice2Of2 _ -> BAD_REQUEST "Missing from"

let getInternalExternalIPOriginVisits (request: HttpRequest) =
    Reports.generateInternalExternalIPOriginVisits (DateTime(2016, 5, 1)) (DateTime(2016, 11, 4))
    |> serializeToJson
    |> OK >=> Writers.setMimeType "application/json; charset=utf-8"

let getPageLoadFrequencyInDateRange (request: HttpRequest) =
    match request.queryParam "from" with
    | Choice1Of2 t -> 
        match request.queryParam "to" with
        | Choice1Of2 u -> 
            match request.queryParam "lowerBoundMilliseconds" with
            | Choice1Of2 v ->
                match request.queryParam "upperBoundMilliseconds" with
                | Choice1Of2 w ->
                    Reports.generatePageLoadFrequencyInDateRange (DateTime.Parse(t)) (DateTime.Parse(u)) (Int32.Parse(v)) (Int32.Parse(w))
                    |> serializeToJson
                    |> OK >=> Writers.setMimeType "application/json; charset=utf-8"
                | Choice2Of2 _ -> BAD_REQUEST "Missing upperBoundMilliseconds"
            | Choice2Of2 _ -> BAD_REQUEST "Missing lowerBoundMilliseconds"            
        | Choice2Of2 _ -> BAD_REQUEST "Missing to"
    | Choice2Of2 _ -> BAD_REQUEST "Missing from"

let getLogRequest (request: HttpRequest) =
    (Agents.logger.PostAndReply LoggerMessage.Retrieve)
    |> Array.reduce (fun acc cur -> acc + "\r\n" + cur)
    |> OK >=> Writers.setMimeType "text/plain; charset=utf-8"

// Included for debugging purposes. Prints all request headers and server
// side-side information related to the request. This WebPart comes in handy 
// when we're looking for a property but are unsure of its name. By dumping
// the context, we can look for its value instead.
let dumpContext: WebPart =
    choose [
        POST >=>
            fun ctx ->
                printfn "%A" ctx
                ctx |> OK "Context dumped" ]

let logger = Loggers.ConsoleWindowLogger LogLevel.Verbose
let app staticFilesPath : WebPart =
    let staticFileRoot = Path.GetFullPath(Environment.CurrentDirectory + staticFilesPath)
    printfn "staticFileRoot: %s" staticFileRoot
    choose [
        //dumpContext
        POST >=> path "/api/collectOnReady" >=> request postOnReady
        POST >=> path "/api/collectOnLoad" >=> request postOnLoad
        path "/" >=> browseFile staticFileRoot "index.html"
        GET >=> path "/api/visitorByVisitorCountInDateRange" >=> request getVisitorByVisitorCountInDateRange
        GET >=> path "/api/internalExternalIPOriginVisitsInDateRange" >=> request getInternalExternalIPOriginVisits
        GET >=> path "/api/pageLoadFrequencyInDateRange" >=> request getPageLoadFrequencyInDateRange
        path "/log" >=> request getLogRequest
        browse staticFileRoot
        RequestErrors.NOT_FOUND "404"
        // pathRegex "(.*)\.(css|png|gif)" >=> Files.browseHome
    ] 
    >=> log logger logFormat
    |> ApplicationInsights.withRequestTracking