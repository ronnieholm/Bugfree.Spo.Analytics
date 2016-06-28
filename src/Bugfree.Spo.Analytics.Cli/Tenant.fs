module Bugfree.Spo.Analytics.Cli.Tenant

open System
open System.Collections.Generic
open System.Security
open Microsoft.SharePoint.Client
open Microsoft.Online.SharePoint.TenantAdministration

let createClientContext (username: string) (password: string) (url: string) =
    let securePassword = new SecureString()
    password.ToCharArray() |> Seq.iter securePassword.AppendChar
    new ClientContext(url, Credentials = SharePointOnlineCredentials(username, securePassword))

let getSiteCollections (username: string) (password: string) (adminUrl: string) =
    let rec getSiteCollectionsRecursive (t: Tenant) (p: List<SiteProperties>) startPosition =
        let ctx = t.Context
        let sites = t.GetSiteProperties(startPosition, false)
        ctx.Load(sites)
        ctx.ExecuteQuery()
            
        sites |> Seq.iter p.Add
        if sites.NextStartIndex = -1 then p else getSiteCollectionsRecursive t p sites.NextStartIndex
        
    let tenantCtx = createClientContext username password adminUrl |> Tenant
    getSiteCollectionsRecursive tenantCtx (List<SiteProperties>()) 0