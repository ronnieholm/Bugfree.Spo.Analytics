module Bugfree.Spo.Analytics.Cli.Program

// See also
//   https://github.com/isaacabraham/fsharp-demonstrator
//   https://github.com/fssnippets/fssnip-website
//   http://eng.localytics.com/exploring-cli-best-practices

open System
open System.IO
open System.Net
open Suave
open Argu

type ServerArgs =
    | [<Mandatory>] Port of number: int
    | [<Mandatory>] Static_files_location of path: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Port _ -> "web server listens for connections on this port."
            | Static_files_location _ -> "path to root folder of file system for serving static files."

type RegisterSiteCollection =
    | [<Mandatory>] Username of username: string
    | [<Mandatory>] Password of password: string
    | [<Mandatory>] Site_collection_url of url: string
    | [<Mandatory>] Analytics_url of url: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Username _ -> "username of the form <user>@<tenant>.onmicrosoft.com."
            | Password _ -> "password matching username."
            | Site_collection_url _ -> "url of the form https://<tenant>.sharepoint.com/<managedPath>/<siteCollectionName>."
            | Analytics_url _ -> "url to analytics endpoint. For an Azure web site it's of the form https://<siteName>.azurewebsites.net."

type RegisterSiteCollections =
    | [<Mandatory>] Username of user: string
    | [<Mandatory>] Password of password: string
    | [<Mandatory>] Tenant_name of name: string
    | [<Mandatory>] Analytics_url of url: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Username _ -> "username of the form <user>@<tenant>.onmicrosoft.com."
            | Password _ -> "password matching username."
            | Tenant_name _ -> "tenant name part of the admin center url of the form https://<tenant>-admin.sharepoint.com."
            | Analytics_url _ -> "url to analytics endpoint. For an Azure web site it's of the form https://<siteName>.azurewebsites.net."

type UnregisterSiteCollection =
    | [<Mandatory>] Username of user: string
    | [<Mandatory>] Password of password: string
    | [<Mandatory>] Site_collection_url of url: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Username _ -> "username of the form <user>@<tenant>.onmicrosoft.com."
            | Password _ -> "password matching username."
            | Site_collection_url _ -> "url of the form https://<tenant>.sharepoint.com/<managedPath>/<siteCollectionName>."

type UnregisterSiteCollections =
    | [<Mandatory>] Username of user: string
    | [<Mandatory>] Password of password: string
    | [<Mandatory>] Tenant_name of name: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Username _ -> "username of the form <user>@<tenant>.onmicrosoft.com."
            | Password _ -> "password matching username."
            | Tenant_name _ -> "tenant name part of the admin center url of the form https://<tenant>-admin.sharepoint.com."

type VerifySiteCollections =
    | [<Mandatory>] Username of user: string
    | [<Mandatory>] Password of password: string
    | [<Mandatory>] Tenant_name of name: string
with
    interface IArgParserTemplate with    
        member this.Usage =
            match this with
            | Username _ -> "username of the form <user>@<tenant>.onmicrosoft.com."
            | Password _ -> "password matching username."
            | Tenant_name _ -> "tenant name part of the admin center url of the form https://<tenant>-admin.sharepoint.com."

type CLIArguments =
    | Server of ParseResults<ServerArgs>
    | Register_site_collection of ParseResults<RegisterSiteCollection>
    | Register_site_collections of ParseResults<RegisterSiteCollections>
    | Unregister_site_collection of ParseResults<UnregisterSiteCollection>
    | Unregister_site_collections of ParseResults<UnregisterSiteCollections>
    | Verify_site_collections of ParseResults<VerifySiteCollections>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Server _ -> "launch web server hosting analytics backend to which SharePoint directs requests."
            | Register_site_collection _ -> "register callback script for a single site collection."
            | Register_site_collections _ -> "register callback script for all tenant site collections."
            | Unregister_site_collection _ -> "unregister callback script for a single site collection."
            | Unregister_site_collections _ -> "unregister callback script for all tenant site collections."
            | Verify_site_collections _ -> "verify number of callback script all tenant site collections."

let parsedArgsOrException args =
    try
        let parser = ArgumentParser.Create<CLIArguments>()
        Choice1Of2 (parser.Parse(args))
    with
    | ex -> Choice2Of2 ex

let getConfig port =
    { defaultConfig with 
        bindings = [ HttpBinding.mk HTTP IPAddress.Loopback port ]        
        listenTimeout = TimeSpan.FromMilliseconds 2000. }

let ApplyToSingle userName password siteCollectionUrl fn =
    try
        use ctx = Tenant.createClientContext userName password siteCollectionUrl
        fn ctx.Site
    with
    | e -> printfn "%A" e        

let ApplyToAll userName password tenantName fn =
    let adminUrl = sprintf "https://%s-admin.sharepoint.com" tenantName
    (Tenant.getSiteCollections userName password adminUrl) |> Seq.iter (fun sc ->
        try
            use ctx = Tenant.createClientContext userName password (sc.Url)
            fn ctx.Site
        with
        | e -> printfn "%A" e)

let examples = """For all subcommands except --server, the provided user must have at least site
collection administrator rights or the operation is skipped for the site.

Examples (place command on single line)

  Enable visitor registration within a single site collections:

  .\Bugfree.Spo.Analytics.Cli.exe
    --register-site-collection
    --username rh@bugfree.onmicrosoft.com
    --password secretPassword
    --site-collection https://bugfree.sharepoint.com/sites/siteCollection
    --analytics-url https://bugfreespoanalytics.azurewebsites.net

  Disable visitor registration within all site collections.

  .\Bugfree.Spo.Analytics.Cli.exe
    --unregister-site-collections
    --username rh@bugfree.onmicrosoft.com
    --password secretPassword
    --tenant-name bugfree

  Start self-hosted web server on port 8083 and serve public files:

  .\Bugfree.Spo.Analytics.Cli.exe --server --port 8083 --static-files-location ..\..\public
"""

[<EntryPoint>]
let main args =
    match parsedArgsOrException args with
    | Choice1Of2 args ->
        match args.TryGetSubCommand() with
        | Some cmd -> 
            match cmd with
            | Server r ->
                let config = getConfig (uint16 (r.GetResult <@ Port @>))
                let appPart = Server.app (Path.Combine((r.GetResult <@ Static_files_location @>), "public"))
                startWebServer config appPart
            | Register_site_collection r ->
                ApplyToSingle 
                    (r.GetResult <@ RegisterSiteCollection.Username @>) 
                    (r.GetResult <@ RegisterSiteCollection.Password @>) 
                    (r.GetResult <@ RegisterSiteCollection.Site_collection_url @>) 
                    (fun ctx -> ScriptRegistration.reset ctx (r.GetResult <@ RegisterSiteCollection.Analytics_url @>))
            | Register_site_collections r -> 
                ApplyToAll
                    (r.GetResult <@ RegisterSiteCollections.Username @>) 
                    (r.GetResult <@ RegisterSiteCollections.Password @>) 
                    (r.GetResult <@ RegisterSiteCollections.Tenant_name @>)
                    (fun ctx -> ScriptRegistration.reset ctx (r.GetResult <@ RegisterSiteCollections.Analytics_url @>))
            | Unregister_site_collection r -> 
                ApplyToSingle
                    (r.GetResult <@ UnregisterSiteCollection.Username @>) 
                    (r.GetResult <@ UnregisterSiteCollection.Password @>) 
                    (r.GetResult <@ UnregisterSiteCollection.Site_collection_url @>) 
                    ScriptRegistration.unregister
            | Unregister_site_collections r -> 
                ApplyToAll
                    (r.GetResult <@ UnregisterSiteCollections. Username @>) 
                    (r.GetResult <@ UnregisterSiteCollections.Password @>) 
                    (r.GetResult <@ UnregisterSiteCollections.Tenant_name @>)
                    ScriptRegistration.unregister
            | Verify_site_collections r -> 
                ApplyToAll 
                    (r.GetResult <@ VerifySiteCollections.Username @>) 
                    (r.GetResult <@ VerifySiteCollections.Password @>) 
                    (r.GetResult <@ VerifySiteCollections.Tenant_name @>)                                
                    ScriptRegistration.validate
        | None -> 
            printfn "%s" (args.Parser.PrintUsage())
            printfn "%s" examples
    | Choice2Of2 ex  ->
        printfn "%s" ex.Message
        printfn "%s" examples

    0
