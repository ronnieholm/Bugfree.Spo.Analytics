module Bugfree.Spo.Analytics.Cli.ScriptRegistration

open System
open Microsoft.SharePoint.Client

let id = "bugfree.spo.analytics"

let unregister (s: Site) =  
    let ctx = s.Context
    printfn "Unregistering %s" ctx.Url

    let actions = s.UserCustomActions
    ctx.Load(actions)
    ctx.ExecuteQuery()
                
    actions 
    |> Seq.filter (fun a -> a.Title = id)
    |> Seq.toArray
    |> Array.iter (fun a -> 
        a.DeleteObject()
        ctx.ExecuteQuery())

let register (s: Site) (analyticsBaseUrl: string) =
    let ctx = s.Context
    printfn "Registering %s" ctx.Url

    let revision = Guid.NewGuid().ToString().Replace("-", "")
    let link = sprintf "%s/%s?rev=%s" analyticsBaseUrl "collector.js" revision           
    let actions = s.UserCustomActions
    ctx.Load(actions)
    ctx.ExecuteQuery()

    let a = actions.Add()
    a.Title <- id
    a.Location <- "ScriptLink"
    a.ScriptBlock <- 
        sprintf """
            var head = document.getElementsByTagName('head')[0]; 
            var script = document.createElement('script');
            script.type = 'text/javascript';
            script.src = '%s';
            head.appendChild(script);""" link
    a.Update()
    ctx.ExecuteQuery()

let reset (s: Site) (analyticsBaseUrl: string) =
    unregister s
    register s analyticsBaseUrl

let validate (s: Site) =
    let ctx = s.Context
    printf "Validating %s" ctx.Url

    let actions = s.UserCustomActions
    ctx.Load(actions)
    ctx.ExecuteQuery()
                          
    actions 
        |> Seq.filter (fun a -> a.Title = id)
        |> fun x ->
            match Seq.length x with
            | 0 -> printfn " -> OK (0)"
            | 1 -> printfn " -> OK (1)"
            | _ as n -> failwith (sprintf "%d actions with id '%s' detected. At most one expected" n id)