module Bugfree.Spo.Analytics.Cli.Configuration

open System
open System.Configuration

type ApplicationInsightsInfo =
    { InstrumentationKey: string }

type VisitorAgentInfo = 
    { CommitThreshold: int }

type Settings = 
    { DatabaseConnectionString: string
      ApplicationInsights: ApplicationInsightsInfo
      VisitorAgent: VisitorAgentInfo }

let getString s = ConfigurationManager.AppSettings.[s: string]
let getConnectionString s = ConfigurationManager.ConnectionStrings.[s: string].ConnectionString |> string

let getSettings() = 
    let getUrl = getString >> Uri
    { DatabaseConnectionString = "BugfreeSpoAnalytics" |> getConnectionString
      ApplicationInsights = { InstrumentationKey = "ApplicationInsightsInstrumentationKey" |> getString }
      VisitorAgent = { CommitThreshold = 5 } }