module Bugfree.Spo.Analytics.Cli.Database

open System
open System.Data.SqlClient
open FSharp.Data

let [<Literal>] compileTimeConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + __SOURCE_DIRECTORY__ + @"\Bugfree.Spo.Analytics.mdf;Integrated Security=True"

type InsertVisitWithPageLoadTime = SqlCommandProvider<"
    insert into [Visits] 
    (CorrelationId, Timestamp, LoginName, Url, PageLoadTime, IP, UserAgent) 
    values 
    (@correlationId, @timestamp, @loginName, @url, @pageLoadTime, @ip, @userAgent)", compileTimeConnectionString>

type InsertVisitWithoutPageLoadTime = SqlCommandProvider<"
    insert into [Visits] 
    (CorrelationId, Timestamp, LoginName, Url, PageLoadTime, IP, UserAgent) 
    values 
    (@correlationId, @timestamp, @loginName, @url, null, @ip, @userAgent)", compileTimeConnectionString>

type SelectByUniqueId = SqlCommandProvider<"
    select *
    from dbo.Visits
    where CorrelationId = @correlationId", compileTimeConnectionString>

type UpdatePageLoadTimeByUniqueId = SqlCommandProvider<"
    update dbo.Visits
    set PageLoadTime = @pageLoadTime
    where CorrelationId = @correlationId", compileTimeConnectionString>

let save runtimeConnectionString (visits: Domain.Visit list): Choice<int, exn> =
    use connection = new SqlConnection(runtimeConnectionString)
    connection.Open()

    use transaction = connection.BeginTransaction()
    let insertWithoutPageLoadTime = new InsertVisitWithoutPageLoadTime(connection, transaction)
    let insertWithPageLoadTime = new InsertVisitWithPageLoadTime(connection, transaction)
    let selectByUniqueId = new SelectByUniqueId(connection, transaction)
    let updateByUniqueId = new UpdatePageLoadTimeByUniqueId(connection, transaction)

    try
        let rows = 
            visits 
            |> List.fold (fun r v ->
                let candidates = selectByUniqueId.Execute(v.CorrelationId) 
                match candidates |> Seq.length with
                // No previous record of visit in database
                | 0 -> 
                    match v.PageLoadTime with
                    | Some t -> 
                        // Visit contains PageLoadTime which means it's a VisitOnLoad message and that
                        // we haven't yet received the corresponding VisitOnReady message.
                        r + insertWithPageLoadTime.Execute(
                            v.CorrelationId, 
                            v.Timestamp, 
                            v.LoginName, 
                            v.Url.ToString(),
                            t,
                            (match v.IP with Some ip -> (ip.ToString()) | None -> null),
                            (match v.UserAgent with Some ua -> ua | None -> null))
                    | None ->
                        // Visit doesn't contain PageLoadTime which means it's a VisitOnReady message
                        // in which case we don't set PageLoadTime, but assume it follows later in
                        // a separate VisitOnLoad message.
                        r + insertWithoutPageLoadTime.Execute(
                            v.CorrelationId, 
                            v.Timestamp, 
                            v.LoginName, 
                            v.Url.ToString(),
                            (match v.IP with Some ip -> (ip.ToString()) | None -> null),
                            (match v.UserAgent with Some ua -> ua | None -> null))
                // A previous record of visit exists in database
                | 1 ->
                    let candidate = candidates |> Seq.head
                    match candidate.PageLoadTime, v.PageLoadTime with
                    | Some _, _ -> 
                        // PageLoadTime is already set, yet we received a request for resetting it
                        // Either the CorrelationId isn't unique or we're somehow processing the same 
                        // message more than once. Better ignore request.
                        r
                    | None, Some t ->
                         // PageLoadTime isn't set. The previous message processed must have been the 
                         // VisitOnReady message without PageLoadTime and now we're processing the
                         // corresponding VisitOnLoad message. Let's update the existing record.
                        r + updateByUniqueId.Execute(t, v.CorrelationId)
                    | _, None ->
                        // Visit being processes doesn't contain PageLoadTime. Are we processing the
                        // same message twice? Better ignore.
                        r
                | _ as times ->
                    let candidate = candidates |> Seq.head
                    failwith (sprintf "Database inconsistency detected. CorrelationId '%s' exists %d times" (candidate.CorrelationId.ToString()) times)
            ) 0
        transaction.Commit()
        Choice1Of2 rows
    with
    | e -> 
        transaction.Rollback()
        Choice2Of2 e