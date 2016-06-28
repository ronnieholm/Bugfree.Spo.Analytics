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
open Suave.Logging
open Suave.Writers
open Agents

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

let processOnReady (request: HttpRequest) =
    let json = PostOnReadyJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    Agents.visitor.Post (Visit {
        CorrelationId = json.CorrelationId
        Timestamp = DateTime.UtcNow
        LoginName = json.LoginName
        Url = json.Url
        PageLoadTime = None
        IP = getXForwardedFor request
        UserAgent = getUserAgent request })
    OK "OnReadyProcessed"

let processOnLoad (request: HttpRequest) =
    let json = PostOnLoadJson.Parse(Encoding.UTF8.GetString(request.rawForm))
    Agents.visitor.Post (Visit {
        CorrelationId = json.CorrelationId
        Timestamp = DateTime.UtcNow
        LoginName = json.LoginName
        Url = json.Url
        PageLoadTime = Some json.PageLoadTime
        IP = getXForwardedFor request
        UserAgent = getUserAgent request })
    OK "OnLoadProcessed"

// Included for debugging purposes. Prints all request headers and server
// side-side information related to the request. The WebPart comes in handy 
// when we're looking for a property but are unsure of its name. By dumping
// the context, we can look for its value instead.
let dumpContext: WebPart =
    choose [
        POST >=>
            fun ctx ->
                printfn "%A" ctx
                ctx |> OK "Context dumped"]

let allowCors : WebPart =
    choose [
        OPTIONS >=>
            fun ctx ->
                ctx |> (                              
                    setHeader "Access-Control-Allow-Origin" "*" >=>
                    setHeader "Access-Control-Allow-Headers" "content-type" >=>
                    OK "CORS approved")]

let logger = Loggers.ConsoleWindowLogger LogLevel.Verbose    
let app staticFilesPath : WebPart =
    let staticFileRoot = Path.GetFullPath(Environment.CurrentDirectory + staticFilesPath)
    printfn "staticFileRoot: %s" staticFileRoot
    choose [
        allowCors
        //dumpContext
        POST >=> path "/api/collectOnReady" >=> request processOnReady
        POST >=> path "/api/collectOnLoad" >=> request processOnLoad
        path "/" >=> browseFile staticFileRoot "index.html"
        browse staticFileRoot
        RequestErrors.NOT_FOUND "404"
    ] 
    >=> log logger logFormat
    |> ApplicationInsights.withRequestTracking