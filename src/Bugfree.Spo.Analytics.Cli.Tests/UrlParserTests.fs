module Bugfree.Spo.Analytics.Cli.Tests.UrlParserTests

open Xunit
open Swensen.Unquote
open Bugfree.Spo.Analytics.Cli.UrlParsers.SpoUrlParser

[<Fact>]
let schemeParser () =
    test <@ parseScheme("https://bugfree-my.sharepoint.com") = (Some Https, "bugfree-my.sharepoint.com") @>
    test <@ parseScheme("http://bugfree-my.sharepoint.com") = (None, "http://bugfree-my.sharepoint.com") @>

    // When a user saves a page containing the JavaScript callback to an MHT file, 
    // opening the saved page for offline viewing triggers a call to our endpoints.
    test <@ parseScheme("file://path-on-local-filesystem") = (None, "file://path-on-local-filesystem")  @>

[<Fact>]
let subdomainParser () =
    test <@ parseSubdomain("bugfree-my.sharepoint.com") = (Some "bugfree-my", ".sharepoint.com") @>
    test <@ parseSubdomain("bugfree-920173322bd717.sharepoint.com") = (Some "bugfree-920173322bd717", ".sharepoint.com") @>
    test <@ parseSubdomain("sharepoint.com") = (None, "sharepoint.com") @>
    test <@ parseSubdomain("com") = (None, "com") @>

[<Fact>]
let domainParser () =
    test <@ parseDomain(".sharepoint.com") = (Some "sharepoint.com", "") @>
    test <@ parseDomain(".sharepoint.com/") = (Some "sharepoint.com", "/") @>
    test <@ parseDomain(".sharepoint.com/sites") = (Some "sharepoint.com", "/sites") @>
    test <@ parseDomain(".com") = (None, ".com") @>

[<Fact>]
let managedPathParser () =
    test <@ parseManagedPath("/sites/aa") = (Some Sites, "/aa") @>
    test <@ parseManagedPath("/teams/aa") = (Some Teams, "/aa") @>
    test <@ parseManagedPath("/sites") = (Some Sites, "") @>
    test <@ parseManagedPath("/sites/") = (Some Sites, "/") @>
    test <@ parseManagedPath("/teams") = (Some Teams, "") @>
    test <@ parseManagedPath("/teams/") = (Some Teams, "/") @>
    test <@ parseManagedPath("/") = (None, "/") @>
    test <@ parseManagedPath("") = (None, "") @>

[<Fact>]
let parseSiteCollectionParser () =
    test <@ parseSiteCollection("/aa") = (Some "aa", "") @>
    test <@ parseSiteCollection("/aa/bb") = (Some "aa", "/bb") @>
    test <@ parseSiteCollection("/aa#") = (Some "aa", "#") @>
    test <@ parseSiteCollection("/aa#e3d21a7c-5eda-43db-8606-af08bf4c77b7") = (Some "aa", "#e3d21a7c-5eda-43db-8606-af08bf4c77b7") @>
    test <@ parseSiteCollection("/aa?") = (Some "aa", "?") @>
    test <@ parseSiteCollection("/aa?e=1") = (Some "aa", "?e=1") @>
    test <@ parseSiteCollection("/%c3%98konomi") = (Some "%c3%98konomi", "") @>
    test <@ parseSiteCollection("/%c3%98konomi?e=1") = (Some "%c3%98konomi", "?e=1") @>
    test <@ parseSiteCollection("/ab.de") = (Some "ab.de", "") @>

[<Fact>]
let combinedParsers () =
    test <@ parse("https://bugfree.sharepoint.com") = 
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com" } @>
    test <@ parse("https://bugfree.sharepoint.com").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com" @>

    test <@ parse("https://bugfree-my.sharepoint.com") = 
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree-my"; Domain = Some "sharepoint.com" } @>
    test <@ parse("https://bugfree-my.sharepoint.com").ComputeSiteCollectionUrl() = Some "https://bugfree-my.sharepoint.com" @>

    test <@ parse("https://bugfree.sharepoint.com/sites/1001520/_layouts/15/mngfield.aspx") = 
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "1001520"; Rest = Some "/_layouts/15/mngfield.aspx" } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/1001520/_layouts/15/mngfield.aspx").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/1001520" @>

    test <@ parse("https://bugfree-36413df1927c26.sharepoint.com/sites/sales") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree-36413df1927c26"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "sales"; Rest = None } @>
    test <@ parse("https://bugfree-36413df1927c26.sharepoint.com/sites/sales").ComputeSiteCollectionUrl() = Some "https://bugfree-36413df1927c26.sharepoint.com/sites/sales" @>

    test <@ parse("https://bugfree.sharepoint.com/sites/sales/lists/SiteCollectionCreationRequests/Alle%20elementer.aspx#InplviewHasha7a9a3ab-ded7-4e1b-b05a-3871d48d3388=") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "sales"; Rest = Some "/lists/SiteCollectionCreationRequests/Alle%20elementer.aspx#InplviewHasha7a9a3ab-ded7-4e1b-b05a-3871d48d3388=" } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/sales/lists/SiteCollectionCreationRequests/Alle%20elementer.aspx#InplviewHasha7a9a3ab-ded7-4e1b-b05a-3871d48d3388=").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/sales" @>

    // Url encoded site collection name
    test <@ parse("https://bugfree.sharepoint.com/sites/%c3%98koentre") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "%c3%98koentre"; Rest = None } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/%c3%98koentre").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/%c3%98koentre" @>

    // Dot in site collection name
    test <@ parse("https://bugfree.sharepoint.com/sites/migrating%201.0%20to%202.0") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "migrating%201.0%20to%202.0"; Rest = None } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/migrating%201.0%20to%202.0").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/migrating%201.0%20to%202.0" @>

    // Enabling publishing features causes site collection name and rest to not always be '/' separated
    test <@ parse("https://bugfree.sharepoint.com/sites/sales#") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "sales"; Rest = Some "#" } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/sales#").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/sales" @>

    test <@ parse("https://bugfree.sharepoint.com/sites/sales#e3d21a7c-5eda-43db-8606-af08bf4c77b7=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#default=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#b7349361-c7c4-4174-a29d-991dec578243=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#99733005-f96d-4880-bfdb-688c917ceb72=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#6ed967e9-fc57-4a15-b752-a29c14b38b95=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#4997c10c-4baf-4f6f-9ef8-7e17ccb0f7da=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = Some Sites; SiteCollection = Some "sales"; Rest = Some "#e3d21a7c-5eda-43db-8606-af08bf4c77b7=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#default=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#b7349361-c7c4-4174-a29d-991dec578243=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#99733005-f96d-4880-bfdb-688c917ceb72=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#6ed967e9-fc57-4a15-b752-a29c14b38b95=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#4997c10c-4baf-4f6f-9ef8-7e17ccb0f7da=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d" } @>
    test <@ parse("https://bugfree.sharepoint.com/sites/sales#e3d21a7c-5eda-43db-8606-af08bf4c77b7=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#default=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#b7349361-c7c4-4174-a29d-991dec578243=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#99733005-f96d-4880-bfdb-688c917ceb72=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#6ed967e9-fc57-4a15-b752-a29c14b38b95=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d#4997c10c-4baf-4f6f-9ef8-7e17ccb0f7da=%7b%22k%22%3a%22%22%2c%22l%22%3a1030%7d").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/sites/sales" @>

    // Tenant root sie collection directly below domain causes no sites or teams managed paths to be present
    test <@ parse("https://bugfree.sharepoint.com/Pictures/Forms/Thumbnails.aspx") = 
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = None; SiteCollection = None; Rest = Some "/Pictures/Forms/Thumbnails.aspx" } @>
    test <@ parse("https://bugfree.sharepoint.com/Pictures/Forms/Thumbnails.aspx").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com" @>

    // Searching outside the current site collection, using the text box in the upper right corner, means the search
    // page displayed is from the Search site collection. That site collection is special and doesn't reside below 
    // any managed path
    test <@ parse("https://bugfree.sharepoint.com/search/Pages/results.aspx?k=test&ql=1030") =
                { emptyResult with Scheme = Some Https; Subdomain = Some "bugfree"; Domain = Some "sharepoint.com"; ManagedPath = None; SiteCollection = Some "search"; Rest = Some "/Pages/results.aspx?k=test&ql=1030" } @>
    test <@ parse("https://bugfree.sharepoint.com/search/Pages/results.aspx?k=test&ql=1030").ComputeSiteCollectionUrl() = Some "https://bugfree.sharepoint.com/search" @>

    test <@ parse "http://bugfree.sharepoint.com" = { Scheme = None; Subdomain = None; Domain = None; ManagedPath = None; SiteCollection = None; Rest = Some "http://bugfree.sharepoint.com" } @>

open System.Data.SqlClient
let integration () =
    // Test against visits in production database to ensure old visits parse to correct site
    // collection names. By parsing old visits, we have a good chance of stumpling upon odd 
    // looking Urls from the wild which the current parser may reject.

    #if INTERACTIVE
    #r "System.Data"
    #endif

    let connection = new SqlConnection("<connection string>")
    connection.Open()
    let sql = """select distinct(v.Url), sc.SiteCollectionUrl 
                 from dbo.Visits v with (nolock), dbo.SiteCollections sc with (nolock) 
                 where v.SiteCollectionId = sc.Id"""
    let command = new SqlCommand(sql, connection)

    // Each command has a timeout separate from the one specified in the connection string
    command.CommandTimeout <- 900
    let r = command.ExecuteReader()

    let mutable i = 0
    let result = ResizeArray<_>()
    while r.Read() do
        i <- i + 1
        if i % 10000 = 0 then printfn "%d" i
        result.Add(r.["Url"] :?> string,  r.["SiteCollectionUrl"] :?> string)

    r.Dispose()
    command.Dispose()
    connection.Dispose()

    result
    |> Seq.iteri(fun i (visitUrl, siteCollectionUrl) ->
        if i % 10000 = 0 then printfn "%d" i
        match parse (visitUrl.ToLower()) with
        | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = Some mp; SiteCollection = Some sc } as parseResult ->
            match parseResult.ComputeSiteCollectionUrl() with
            | Some url ->
                if siteCollectionUrl <> url 
                then printfn "ComputeSiteCollectionUrl failure: %s - %s" siteCollectionUrl url
            | None -> failwith "Should never happen"
        
            if sd.Contains("-")
            then printfn "Dash not supported: %s - %s - %s" visitUrl siteCollectionUrl sc
        | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = None } ->
            // Visits on tenant root site collection
            //printfn "%s" visitUrl
            ()
        | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = Some sc } ->
            // Visists on search site collection
            if sc <> "search" then printfn "Search failure: %s - %s - %s" visitUrl siteCollectionUrl sc
        | _ ->
            // Ending up here indicates a Url couldn't be parsed. The Url shouldn't be in the
            // database. It's likely a leftover from a previous parser implementation in which 
            // case the visit should be deleted or migrated.
            printfn "Unsupported url: %s - %s" visitUrl siteCollectionUrl)
