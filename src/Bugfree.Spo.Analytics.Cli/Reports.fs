module Bugfree.Spo.Analytics.Cli.Reports

// Simulate an OLAP cube?
// https://www.youtube.com/watch?v=yoE6bgJv08E
// https://www.youtube.com/watch?v=1Qdf5c_nmtw (OLTP vs OLAP)
// Use O365 reports as a baseline for laying out the report page
//  Office 365 Adoption Content Pack

open System
open System.Data.SqlClient
open System.Text.RegularExpressions
open FSharp.Data

let settings = Configuration.getSettings()
let reports = settings.Reports

type VisitorByVisitorCountInDateRange =
    { LoginName: string
      VisitCount: int }

type UniqueUserCountInPeriod =
    { OnPremise: int
      InCloud: int
      External: int
      Other: int }

type VisitCountInPeriod =
    { OnPremise: int
      InCloud: int
      External: int
      Other: int }

type VisitorsByVisitorCount =
    { From: DateTime
      To: DateTime
      UniqueUserCountInPeriod: UniqueUserCountInPeriod
      VisitCountInPeriod: VisitCountInPeriod }

let emptyUniqueUserCountInPeriod: UniqueUserCountInPeriod =
    { OnPremise = 0; InCloud = 0; External = 0; Other = 0 }

let emptyVisitCountsInPeriod: VisitCountInPeriod = 
    { OnPremise = 0; InCloud = 0; External = 0; Other = 0 }
        
type InternalExternalIPOriginVisits =
    { From: DateTime
      To: DateTime
      Internal: int
      External: int }

type PageLoadFrequencyVisits =
    { From: DateTime
      To: DateTime
      lowerBoundMilliseconds: int
      upperBoundMilliseconds: int
      Internal: int
      External: int }    

module Repository =
    let toInClauseList (ips: string[]) =
        ips         
        |> Array.map (fun ip -> sprintf "'%s'" ip)
        |> Array.reduce (fun acc cur -> sprintf "%s, %s" acc cur)

    let getVisitorsByVisitorCountInDateRange (fromDate: DateTime) (toDate: DateTime): VisitorByVisitorCountInDateRange[] =
        let sql =
            "select ln.LoginName, count(*) VisitCount 
             from Visits v with (nolock), LoginNames ln with (nolock) 
             where v.Timestamp >= @fromDate and v.Timestamp < @toDate
             and v.LoginNameId = ln.Id
             group by ln.LoginName"

        use connection = new SqlConnection(settings.DatabaseConnectionString)        
        use command = new SqlCommand(sql, connection)
        command.Parameters.AddWithValue("@fromDate", fromDate) |> ignore
        command.Parameters.AddWithValue("@toDate", toDate) |> ignore
        connection.Open()
        use r = command.ExecuteReader()
        let result = ResizeArray<_>()

        while r.Read() do
            result.Add({ LoginName = r.["LoginName"] :?> string; VisitCount = r.["VisitCount"] :?> int })
        result.ToArray()

    let getInsideVisitCountInDateRange (fromDate: DateTime) (toDate: DateTime): int =
        let sql =
            sprintf
                "select count(*) InternalVisits
                 from Visits v with (nolock), IPs i with (nolock)
                 where v.Timestamp >= @fromDate and v.Timestamp < @toDate
                 and i.IP in (%s)
                 and v.IPId = i.Id" (reports.CompanyPublicIPs |> toInClauseList)

        use connection = new SqlConnection(settings.DatabaseConnectionString)
        use command = new SqlCommand(sql, connection)
        command.Parameters.AddWithValue("@fromDate", fromDate) |> ignore
        command.Parameters.AddWithValue("@toDate", toDate) |> ignore
        connection.Open()
        command.ExecuteScalar() :?> int

    let getOutsideVisitCountInDateRange (fromDate: DateTime) (toDate: DateTime): int =
        let sql =
            sprintf
                "select count(*) InternalVisits
                 from Visits v with (nolock), IPs i with (nolock)
                 where v.Timestamp >= @fromDate and v.Timestamp < @toDate
                 and i.IP not in (%s)
                 and v.IPId = i.Id" (reports.CompanyPublicIPs |> toInClauseList)

        use connection = new SqlConnection(settings.DatabaseConnectionString)
        use command = new SqlCommand(sql, connection)
        command.Parameters.AddWithValue("@fromDate", fromDate) |> ignore
        command.Parameters.AddWithValue("@toDate", toDate) |> ignore
        connection.Open()
        command.ExecuteScalar() :?> int

    let getInsidePageLoadFrequencyInDateRange (fromDate: DateTime) (toDate: DateTime) (lowerBoundMilliseconds: int) (upperBoundMilliseconds: int): int =
        let sql = 
            sprintf 
                "select count(*) Visits
                 from Visits v with (nolock), IPs i with (nolock)
                 where PageLoadTime is not null               
                 and PageLoadTime >= @lowerBoundMilliseconds
                 and PageLoadTime < @upperBoundMilliseconds
                 and v.Timestamp >= @fromDate
                 and v.Timestamp < @toDate              
                 and i.IP in (%s)
                 and v.IPId = i.Id" (reports.CompanyPublicIPs |> toInClauseList)
        
        use connection = new SqlConnection(settings.DatabaseConnectionString)
        use command = new SqlCommand(sql, connection)
        command.Parameters.AddWithValue("@lowerBoundMilliseconds", lowerBoundMilliseconds) |> ignore
        command.Parameters.AddWithValue("@upperBoundMilliseconds", upperBoundMilliseconds) |> ignore
        command.Parameters.AddWithValue("@fromDate", fromDate) |> ignore
        command.Parameters.AddWithValue("@toDate", toDate) |> ignore
        connection.Open()
        command.ExecuteScalar() :?> int

    let getOutsidePageLoadFrequencyInDateRange (fromDate: DateTime) (toDate: DateTime) (lowerBoundMilliseconds: int) (upperBoundMilliseconds: int): int =
        let sql = 
            sprintf 
                "select count(*) Visits
                 from Visits v with (nolock), IPs i with (nolock)
                 where PageLoadTime is not null               
                 and PageLoadTime >= @lowerBoundMilliseconds
                 and PageLoadTime < @upperBoundMilliseconds
                 and v.Timestamp >= @fromDate
                 and v.Timestamp < @toDate              
                 and i.IP not in (%s)
                 and v.IPId = i.Id" (reports.CompanyPublicIPs |> toInClauseList)
        
        use connection = new SqlConnection(settings.DatabaseConnectionString)
        use command = new SqlCommand(sql, connection)
        command.Parameters.AddWithValue("@lowerBoundMilliseconds", lowerBoundMilliseconds) |> ignore
        command.Parameters.AddWithValue("@upperBoundMilliseconds", upperBoundMilliseconds) |> ignore
        command.Parameters.AddWithValue("@fromDate", fromDate) |> ignore
        command.Parameters.AddWithValue("@toDate", toDate) |> ignore
        connection.Open()
        command.ExecuteScalar() :?> int

let (|OnPremise|External|InCloud|Other|) (loginName: string) =
    if Regex(sprintf "@%s$" reports.OnPremiseDomain).IsMatch(loginName) then OnPremise
    elif Regex(sprintf ".?#ext#@%s.onmicrosoft.com" reports.InCloudDomain).IsMatch(loginName) then External
    elif Regex(sprintf "@%s.onmicrosoft.com" reports.InCloudDomain).IsMatch(loginName) then InCloud
    else (*printfn "%s" loginName;*) Other

// We could have the database do more work, if upon save we determined the usertype
// and saved in column by itself. Then the code below would be needed, albeit it would
// have to be moved inside the Save agent instead.
let generateVisitorsByVisitorCount (from: DateTime) (toX: DateTime) =  
    let visitorsByVisitorCount = Repository.getVisitorsByVisitorCountInDateRange from toX
    let uniqueUserCounts =
        visitorsByVisitorCount
        |> Array.fold (fun (acc: UniqueUserCountInPeriod) cur -> 
            match cur.LoginName.ToLower() with
            | OnPremise -> { acc with OnPremise = acc.OnPremise + 1 }
            | InCloud -> { acc with InCloud = acc.InCloud + 1 }
            | External -> { acc with External = acc.External + 1 }
            | Other -> { acc with Other = acc.Other + 1 }) emptyUniqueUserCountInPeriod

    let visitCounts =
        visitorsByVisitorCount
        |> Array.fold (fun (acc: VisitCountInPeriod) cur ->
            match cur.LoginName.ToLower() with
            | OnPremise -> { acc with OnPremise = acc.OnPremise + cur.VisitCount }
            | InCloud -> { acc with InCloud = acc.InCloud + cur.VisitCount }
            | External -> { acc with External = acc.External + cur.VisitCount }
            | Other -> { acc with Other = acc.Other + cur.VisitCount }) emptyVisitCountsInPeriod

    { From = from; To = toX; UniqueUserCountInPeriod = uniqueUserCounts; VisitCountInPeriod = visitCounts }

let generatePageLoadFrequencyInDateRange (from: DateTime) (toX: DateTime) (lowerBoundMilliseconds: int) (upperBoundMilliseconds: int): PageLoadFrequencyVisits =
    let inside = Repository.getInsidePageLoadFrequencyInDateRange from toX lowerBoundMilliseconds upperBoundMilliseconds
    let outside = Repository.getOutsidePageLoadFrequencyInDateRange from toX lowerBoundMilliseconds upperBoundMilliseconds
    { From = from; To = toX; lowerBoundMilliseconds = lowerBoundMilliseconds; upperBoundMilliseconds = upperBoundMilliseconds; Internal = inside; External = outside }

let generateInternalExternalIPOriginVisits (from: DateTime) (toX: DateTime): InternalExternalIPOriginVisits =
    let inside = Repository.getInsideVisitCountInDateRange from toX
    let outside = Repository.getOutsideVisitCountInDateRange from toX
    { From = from; To = toX; Internal = inside; External = outside }