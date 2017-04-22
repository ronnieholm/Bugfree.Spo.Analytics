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
open UrlParsers
open UrlParsers.SpoUrlParser
open Utils

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

let getXForwardedForHeader (request: HttpRequest) =
    match (request.header "x-forwarded-for") with 
    | Choice1Of2 xff -> Utils.parseOriginatingIPFromXForwardedForHeader xff 
    | Choice2Of2 _ -> failwith "Missing x-forwarded-for"

let filterVisitorUrl (url: string) =
    match SpoUrlParser.parse url with
    | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = Some mp; SiteCollection = Some sc; Rest = _ } as r ->
        if sd.Contains("-") then None else r.ComputeSiteCollectionUrl()
    | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = None; Rest = rest } as r ->
        // Visit on tenant site collection.
        // It makes little sense to record visits to https://<tenant>-my/sharepoint.com because
        // it redirects to the user's actual mysite on which we don't record visits. Similarly, 
        // little value is gained from recording visits on application webs such as 
        // https://<tenant>-36413df1927c26.sharepoint.com. Appearently this gets recorded as part
        // of redirection similar to the mysite.
        if sd.Contains("-") then None else r.ComputeSiteCollectionUrl()
    | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = Some sc; Rest = _ } as r ->
        // Visists on search site collection
        if sd.Contains("-") || sc <> "search" then None else r.ComputeSiteCollectionUrl()
    | _ ->
        // Ending up here indicates a Url which couldn't be parsed. The Url shouldn't 
        // be in the database in the first place. It's likely a left over from a 
        // previous parser implementation in which case it the visit should be deleted 
        // or migrated.
        None

let postOnReady (request: HttpRequest) =
    let json = PostOnReadyJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    let visitUrl = json.Url.ToLower()
    match filterVisitorUrl visitUrl with
    | Some sc ->
        Agents.visitor.Post (Visit {
            CorrelationId = json.CorrelationId
            Timestamp = DateTime.UtcNow
            LoginName = json.LoginName
            SiteCollectionUrl = sc
            VisitUrl = visitUrl
            PageLoadTime = None
            IP = getXForwardedForHeader request |> IPAddress.Parse
            UserAgent = getUserAgent request })
        OK "processedOnReady"
    | None -> 
        Agents.logger.Post (Message(sprintf "Skipping visit: '%s'" visitUrl))
        OK "invalidVisitUrl"

let postOnLoad (request: HttpRequest) =
    let json = PostOnLoadJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    let visitUrl = json.Url.ToLower()
    match filterVisitorUrl visitUrl with
    | Some sc ->
        Agents.visitor.Post (Visit {
            CorrelationId = json.CorrelationId
            Timestamp = DateTime.UtcNow
            LoginName = json.LoginName
            SiteCollectionUrl = sc
            VisitUrl = visitUrl
            PageLoadTime = Some json.PageLoadTime
            IP = getXForwardedForHeader request |> IPAddress.Parse
            UserAgent = getUserAgent request })
        OK "processedOnLoad"
    | None -> 
        Agents.logger.Post (Message(sprintf "Skipping visit: '%s'" visitUrl))
        OK "invalidVisitUrl"

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