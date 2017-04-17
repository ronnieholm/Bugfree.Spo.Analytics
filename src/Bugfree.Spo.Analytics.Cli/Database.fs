module Bugfree.Spo.Analytics.Cli.Database

open System
open System.Data.SqlClient
open System.Text.RegularExpressions
open FSharp.Data
open Bugfree.Spo.Analytics.Cli.Utils

let [<Literal>] compileTimeConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + __SOURCE_DIRECTORY__ + @"\Bugfree.Spo.Analytics.mdf;Integrated Security=True"

type GetSiteCollection = SqlCommandProvider<"select Id from SiteCollections where SiteCollectionUrl = @siteCollectionUrl", compileTimeConnectionString>
type GetLoginName = SqlCommandProvider<"select Id from LoginNames where LoginName = @loginName", compileTimeConnectionString>
type GetIP = SqlCommandProvider<"select Id from IPs where IP = @ip", compileTimeConnectionString>
type GetUserAgent = SqlCommandProvider<"select Id from UserAgents where UserAgent = @userAgent", compileTimeConnectionString>

type InsertSiteCollectionUrl = SqlCommandProvider<"
    insert into SiteCollections (SiteCollectionUrl) values (@siteCollectionUrl)
    select scope_identity() Id", compileTimeConnectionString>

type InsertLoginName = SqlCommandProvider<"
    insert into LoginNames (LoginName) values (@loginName)
    select scope_identity() Id", compileTimeConnectionString>

type InsertIP = SqlCommandProvider<"
    insert into IPs (IP) values (@ip)
    select scope_identity() Id", compileTimeConnectionString>

type InsertUserAgent = SqlCommandProvider<"
    insert into UserAgents (UserAgent) values (@userAgent)
    select scope_identity() Id", compileTimeConnectionString>

// The type provider doesn't directly support inserting into possible null columns like 
// pageLoadTime and userAgentId. Instead we would have to create types for InsertWithPageLoadTime
// and InsertWithoutPageLoadTime, and similarly for UserAgent. To handle every permutation we'd
// require four insert statements and similar switching logic at the call site. We therefore
// opt for parameterized, dynamic SQL instead.
let insertVisitSql = "
    insert into Visits
    (CorrelationId, Timestamp, Url, PageLoadTime, SiteCollectionId, LoginNameId, IPId, UserAgentId) 
    values 
    (@correlationId, @timestamp, @url, @pageLoadTime, @siteCollectionId, @loginNameId, @ipId, @userAgentId)"

type SelectByUniqueId = SqlCommandProvider<"
    select *
    from dbo.Visits
    where CorrelationId = @correlationId", compileTimeConnectionString>

type UpdatePageLoadTimeByUniqueId = SqlCommandProvider<"
    update dbo.Visits
    set PageLoadTime = @pageLoadTime
    where CorrelationId = @correlationId", compileTimeConnectionString>

let getOrCreateLoginName (c: SqlConnection) (t: SqlTransaction) (loginName: string) =
    let getLoginName = new GetLoginName(c, t)
    let insertLoginName = new InsertLoginName(c, t)
    let loginNames = getLoginName.Execute(loginName) |> Seq.toArray
    
    match loginNames |> Array.length with
    | 0 -> 
        let id = insertLoginName.Execute(loginName) |> Seq.exactlyOne
        match id with
        | Some x -> x |> int
        | None -> failwith "Insertion of LoginName must return new row id"
    | 1 -> loginNames |> Seq.exactlyOne
    | _ as times -> failwithf "Database inconsistency detected. LoginName '%s' exists %d times" loginName times

let getOrCreateIP (c: SqlConnection) (t: SqlTransaction) (ip: string) =
    let getIP = new GetIP(c, t)
    let insertIP = new InsertIP(c, t)
    let ips = getIP.Execute(ip) |> Seq.toArray

    match ips |> Array.length with
    | 0 -> 
        let id = insertIP.Execute(ip) |> Seq.exactlyOne
        match id with
        | Some x -> x |> int
        | None -> failwith "Insertion of IP must return new row id"
    | 1 -> ips |> Seq.exactlyOne
    | _ as times -> failwithf "Database inconsistency detected. IP %s exists %d times" ip times

let getOrCreateUserAgent (c: SqlConnection) (t: SqlTransaction) (userAgent: string) =
    let getUserAgent = new GetUserAgent(c, t)
    let insertUserAgent = new InsertUserAgent(c, t)
    let userAgents = getUserAgent.Execute(userAgent) |> Seq.toArray

    match userAgents |> Array.length with
    | 0 -> 
        let id = insertUserAgent.Execute(userAgent) |> Seq.exactlyOne
        match id with
        | Some x -> x |> int
        | None -> failwith "Insertion of UserAgent must return new row id"
    | 1 -> userAgents |> Seq.exactlyOne
    | _ as times -> failwithf "Database inconsistency detected. UserAgent '%s' exists %d times" userAgent times

let getOrCreateSiteCollection (c: SqlConnection) (t: SqlTransaction) (siteCollectionUrl: string) =
    let getSiteCollection = new GetSiteCollection(c, t)    
    let InsertSiteCollectionUrl = new InsertSiteCollectionUrl(c, t)
    let siteCollections = getSiteCollection.Execute(siteCollectionUrl) |> Seq.toArray

    match siteCollections |> Array.length with
    | 0 -> 
        let id = InsertSiteCollectionUrl.Execute(siteCollectionUrl) |> Seq.exactlyOne
        match id with
        | Some x -> x |> int
        | None -> failwith "Insertion of SiteCollectionUrl must return new row id"
    | 1 -> siteCollections |> Seq.exactlyOne
    | _ as times -> failwithf "Database inconsistency detected. SiteCollectionUrl '%s' exists %d times" siteCollectionUrl times

let save runtimeConnectionString (visits: Domain.Visit list): Choice<int, exn> =
    use connection = new SqlConnection(runtimeConnectionString)
    connection.Open()

    use transaction = connection.BeginTransaction()
    let selectByUniqueId = new SelectByUniqueId(connection, transaction)
    let updateByUniqueId = new UpdatePageLoadTimeByUniqueId(connection, transaction)
    let getOrCreateSiteCollection' = memoize(getOrCreateSiteCollection connection transaction)
    let getOrCreateLoginName' = memoize(getOrCreateLoginName connection transaction)
    let getOrCreateIP' = memoize(getOrCreateIP connection transaction)
    let getOrCreateUserAgent' = memoize(getOrCreateUserAgent connection transaction)
    
    try
        let rows =
            visits
            |> List.fold (fun r v ->
                let candidates = selectByUniqueId.Execute(v.CorrelationId)
                match candidates |> Seq.length with

                // No previous record of visit in database                
                | 0 ->
                    let siteCollectionId = getOrCreateSiteCollection' v.SiteCollectionUrl
                    let loginNameId = getOrCreateLoginName' v.LoginName
                    let ipId = getOrCreateIP' (v.IP.ToString())
                    let userAgentId = 
                        match v.UserAgent with
                        | Some ua -> Some (getOrCreateUserAgent' ua)
                        | None -> None

                    use cmd = new SqlCommand(insertVisitSql, connection, transaction)
                    cmd.Parameters.AddWithValue("@correlationId", v.CorrelationId) |> ignore
                    cmd.Parameters.AddWithValue("@timestamp", v.Timestamp) |> ignore
                    cmd.Parameters.AddWithValue("@url", v.VisitUrl) |> ignore

                    match v.PageLoadTime with
                    | Some t -> cmd.Parameters.AddWithValue("@pageLoadTime", t) |> ignore
                    | None -> cmd.Parameters.AddWithValue("@pageLoadTime", DBNull.Value) |> ignore

                    cmd.Parameters.AddWithValue("@siteCollectionId", siteCollectionId) |> ignore
                    cmd.Parameters.AddWithValue("@loginNameId", loginNameId) |> ignore
                    cmd.Parameters.AddWithValue("@ipId", ipId) |> ignore

                    match userAgentId with
                    | Some ua -> cmd.Parameters.AddWithValue("@userAgentId", ua) |> ignore
                    | None -> cmd.Parameters.AddWithValue("@userAgentId", DBNull.Value) |> ignore      

                    r + cmd.ExecuteNonQuery()

                // A previous record of this visit exists in database
                | 1 -> 
                    let candidate = candidates |> Seq.head
                    match candidate.PageLoadTime, v.PageLoadTime with
                    | Some _, _ -> 
                        // PageLoadTime is already set, yet we received a request for resetting it
                        // Either the CorrelationId isn't unique or we're processing the same message 
                        // more than once. Better ignore request.
                        r
                    | None, Some t ->
                         // PageLoadTime isn't set. A previous message must have been the VisitOnReady 
                         // message without PageLoadTime and now we're processing the corresponding 
                         // VisitOnLoad message. Let's update the existing record.
                        r + updateByUniqueId.Execute(t, v.CorrelationId)
                    | _, None ->
                        // Visit being processes doesn't contain PageLoadTime. Are we processing the
                        // same message twice? Better ignore request.
                        r                   
                | _ as times ->
                    let candidate = candidates |> Seq.head
                    failwith (sprintf "Database inconsistency detected. CorrelationId '%s' exists %d times" (candidate.CorrelationId.ToString()) times)
            ) 0
        transaction.Commit()
        Choice1Of2 rows
    with
    | e -> 
        // TODO: if a visit causes this message to fail, move message to dead letter queue (special entry 
        // in log file) or it'll forever block message processing.
        transaction.Rollback()
        Choice2Of2 e