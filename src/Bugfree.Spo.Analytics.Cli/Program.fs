module Bugfree.Spo.Analytics.Cli.Program

// See also
//   https://github.com/isaacabraham/fsharp-demonstrator
//   https://github.com/fssnippets/fssnip-website
//   http://eng.localytics.com/exploring-cli-best-practices

open System
open System.IO
open System.Net
open Suave

let getConfig port =
    { defaultConfig with 
        bindings = [ HttpBinding.mk HTTP IPAddress.Loopback port ]        
        listenTimeout = TimeSpan.FromMilliseconds 2000. }

[<EntryPoint>]
let main args =
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

    match args |> Array.toList with
    | ["--help"] ->
        printfn "--server <port> <relativeStaticFilesLocation>"
        printfn ""
        printfn "  Start the self-hosted web server, usually triggered by the Azure App"
        printfn "  Service and its use of Azure's httpPlatformHandler."
        printfn ""
        printfn "--register-site-collection <userName> <password> <siteCollectionUrl> <analyticsBaseUrl>"
        printfn "--unregister-site-collection <userName> <password> <siteCollectionUrl>"
        printfn ""
        printfn "  Enable or disable visitor registration within a site collection." 
        printfn ""
        printfn "--register-site-collections <userName> <password> <tenantName> <analyticsBaseUrl>"
        printfn "--unregister-site-collections <userName> <password> <tenantName>"
        printfn ""
        printfn "  Enable or disable visitor registration across all site collections."
        printfn ""
        printfn "--verify-site-collections <userName> <password> <tenantName>"
        printfn ""
        printfn "  Report on the number of visitor registrations with each site"
        printfn "  collection (0, 1, or error). At most one registration must be present"
        printfn "  or visits are recorded multiple times. This operation is included for"
        printfn "  debugging purposes."
        printfn ""
        printfn "For all operations, the provided user must have at least site collection"
        printfn "administrator rights or the particular registration is skipped."
        printfn ""
        printfn "Examples (place command on single line)"
        printfn ""
        printfn "  Enable visitor registration on a single site collections:"
        printfn ""
        printfn "  .\Bugfree.Spo.Analytics.Cli.exe" 
        printfn "    --register-site-collection" 
        printfn "      rh@bugfree.onmicrosoft.com"
        printfn "      secretPassword" 
        printfn "      https://bugfree.sharepoint.com/sites/siteCollection"
        printfn "      https://bugfreespoanalytics.azurewebsites.net"
        printfn ""
        printfn "  (Command outputs URLs of site collections as it attempts to enable"
        printfn "   visitor registration. Errors, such as no access, are displayed as well.)"
        printfn ""
        printfn "  Disable visitor registration on all site collections."
        printfn ""
        printfn "  .\Bugfree.Spo.Analytics.Cli.exe" 
        printfn "    --unregister-site-collections"
        printfn "      rh@bugfree.onmicrosoft.com"
        printfn "      secretPassword"
        printfn "      bugfree"
        printfn ""
        printfn "  Start self-hosted web server on port 8083 and serve public files:"
        printfn ""
        printfn " .\Bugfree.Spo.Analytics.Cli.exe --server 8083 ..\..\public"
        printfn ""
    | ["--server"; port; staticFilesLocation] -> 
        let config = getConfig (uint16 port)
        let appPart =  Bugfree.Spo.Analytics.Cli.Server.app (Path.Combine(staticFilesLocation, "public"))
        startWebServer config appPart
    | ["--register-site-collection"; userName; password; siteCollectionUrl; analyticsBaseUrl] ->
        ApplyToSingle userName password siteCollectionUrl (fun ctx -> ScriptRegistration.reset ctx analyticsBaseUrl)
    | ["--register-site-collections"; userName; password; tenantName; analyticsBasePath] ->
        ApplyToAll userName password tenantName (fun ctx -> ScriptRegistration.reset ctx analyticsBasePath)
    | ["--unregister-site-collection"; userName; password; siteCollectionUrl] ->
        ApplyToSingle userName password siteCollectionUrl ScriptRegistration.unregister
    | ["--unregister-site-collections"; userName; password; tenantName] ->
        ApplyToAll userName password tenantName ScriptRegistration.unregister
    | ["--verify-site-collections"; userName; password; tenantName] ->
        ApplyToAll userName password tenantName ScriptRegistration.validate
    | _ -> printfn "Invalid arguments. Use --help for overview of arguments"

    0
