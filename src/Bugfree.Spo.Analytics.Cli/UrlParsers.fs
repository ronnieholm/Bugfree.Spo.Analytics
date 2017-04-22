module Bugfree.Spo.Analytics.Cli.UrlParsers

(*
    Implements a parser for SharePoint Online Urls. The reason for such parser is that not all
    all Urls follow the pattern of https://tenant/managedPath/siteCollection/rest. With publishing 
    features enabled, for instance, site collection doesn't always contain a trailing slash. Also,
    the parser makes it simpler for disregard application web and my site Urls. Those add little 
    value to the analysis anyway. Finally, the parser takes into accounts visits on the tenant
    root site collection as well as the search site collection, which also doesn't follow the 
    basic Url pattern as the managed path is missing.

    The input Url is assumed to be canonicalized to lower case.
*)

open System
open System.Text.RegularExpressions

module SpoUrlParser =
    let https = Regex("^https://", RegexOptions.Compiled)
    let subdomain = Regex("^(?<sd>[a-z0-9\-]+)\.[a-z0-9]+\.[a-z0-9]+", RegexOptions.Compiled)
    let domain = Regex("^\.(?<d>[a-z0-9]+\.[a-z0-9]+)", RegexOptions.Compiled)
    let managedPath = Regex("^/(?<m>[a-z]+)/*", RegexOptions.Compiled)
    let siteCollection = Regex("^/(?<sc>[a-z0-9\-_%\.]+)/*")

    // Only HTTPS is supported with SPO
    type Scheme = 
        | Https
        override this.ToString() =
            match this with
            | Https -> "https"

    type ManagedPath =
        | Teams
        | Sites
        override this.ToString() =
            match this with
            | Teams -> "teams"
            | Sites -> "sites"

    type Result =
        { Scheme: Scheme option
          Subdomain: string option
          Domain: string option
          ManagedPath: ManagedPath option
          SiteCollection: string option
          Rest: string option }
        member this.ComputeSiteCollectionUrl() =
            match this with
            | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = Some mp; SiteCollection = Some sc; Rest = _ } ->
                Some (sprintf "%s://%s.%s/%s/%s" (Https.ToString()) sd d (mp.ToString()) sc)
            | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = None; Rest = _ } ->
                Some (sprintf "%s://%s.%s" (Https.ToString()) sd d)
            | { Scheme = Some Https; Subdomain = Some sd; Domain = Some d; ManagedPath = None; SiteCollection = Some sc; Rest = _ } ->
                Some (sprintf "%s://%s.%s/%s" (Https.ToString()) sd d sc)
            | _ -> None
    
    let emptyResult =
        { Scheme = None; Subdomain = None; Domain = None;
          ManagedPath = None; SiteCollection = None; Rest = None }

    let parseScheme (s: string) = 
        let m = https.Match(s)
        if m.Success then (Some Https, s.Substring(m.Length))
        else (None, s)

    let parseSubdomain (s: string) =
        let m = subdomain.Match(s)
        if m.Success 
        then (Some (m.Groups.["sd"].Value), s.Substring(m.Groups.["sd"].Length))
        else (None, s)

    let parseDomain (s: string) =
        let m = domain.Match(s)
        if m.Success 
        then (Some (m.Groups.["d"].Value), s.Substring(m.Groups.["d"].Length + 1))
        else (None, s)

    let parseManagedPath (s: string) =        
        let m = managedPath.Match(s)
        if m.Success then
            let m2 = m.Groups.["m"]
            let r = s.Substring(m2.Length + 1)
            match m2.Value with
            | "sites" -> (Some Sites, r)
            | "teams" -> (Some Teams, r)
            | _ -> (None, s)
        else (None, s)

    let parseSiteCollection (s: string) =
        // According to SharePoint's create site collection dialog, site collection Urls cannot
        // contain any of the following characters: " # % * : < > ? \ / |
        // 
        // However, they can contain Url encoded charactes which make use of the % characters. 
        // For instance, here's a couple of example of encoded characters:
        //
        // space -> %20
        // æ -> %C3%A6, Æ -> %C3%86
        // ø -> %C3%B8, Ø -> %C3%98
        // å -> %C3%A5, Å -> %C3%85
        // æ ø å -> %C3%A6%20%C3%B8%20%C3%A5
        // 
        // Thus, we allow % to occur in the pattern. The alternative would be to first Url decode 
        // and then include the special characters in the pattern (or create a pattern not including
        // the characters listed by the create dialog). The former seems the simplest approach. 
        // While we're at it, we also include the period as that's been observed in the wild.
        let m = siteCollection.Match(s)
        if m.Success 
        then (Some (m.Groups.["sc"].Value), s.Substring(m.Groups.["sc"].Value.Length + 1))
        else (None, s)

    let parse (u: string) =
        let (scheme, r) = parseScheme u
        let (subdomain, r1) = parseSubdomain r
        let (domain, r2) = parseDomain r1
        let (managedPath, r3) = parseManagedPath r2

        let common = { emptyResult with Scheme = scheme; Subdomain = subdomain; Domain = domain; ManagedPath = managedPath }
        match managedPath with
        | Some _ -> 
            let (siteCollection, r4) = parseSiteCollection r3
            { common with SiteCollection = siteCollection; Rest = if r4.Length > 0 then Some r4 else None }
        | None ->
            // The search and tenant root site collecion https://<tenant>.sharepoint.com should 
            // be the only site collections without a managed path.
            let search = "/search"
            if r3.StartsWith(search) 
            then 
                { common with 
                      SiteCollection = Some "search"
                      Rest = if r3.Length > search.Length then Some (r3.Substring(search.Length)) else None }
            else
                { common with
                      SiteCollection = None
                      Rest = if r3.Length > 0 then Some r3 else None }