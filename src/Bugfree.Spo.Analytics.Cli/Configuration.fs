module Bugfree.Spo.Analytics.Cli.Configuration

open System
open System.Configuration
open Bugfree.Spo.Analytics.Cli.AzureConfiguration

type ApplicationInsightsInfo =
    { InstrumentationKey: string }

type VisitorAgentInfo = 
    { CommitThreshold: int }

type ReportsInfo =
    { InCloudDomain: string
      OnPremiseDomain: string
      CompanyPublicIPs: string[] }

type Settings = 
    { DatabaseConnectionString: string
      ApplicationInsights: ApplicationInsightsInfo
      VisitorAgent: VisitorAgentInfo
      Reports: ReportsInfo }

let getInt s = ConfigurationManager.AppSettings.[s: string] |> int
let getString s = ConfigurationManager.AppSettings.[s: string]
let getDateTime s = DateTime.Parse(getString s)
let getConnectionString s = ConfigurationManager.ConnectionStrings.[s: string].ConnectionString |> string

let getStringArray s =   
    let value = getString s    
    value.Split([|','|])
    |> Array.map (fun e -> e.Trim())

let getSettings() = 
    applyAzureEnvironmentToConfigurationManager()
    let getUrl = getString >> Uri
    { DatabaseConnectionString = getConnectionString "BugfreeSpoAnalytics"
      ApplicationInsights = { InstrumentationKey = getString "ApplicationInsightsInstrumentationKey" }
      VisitorAgent = { CommitThreshold = getInt "CommitThreshold" }
      Reports = 
        { InCloudDomain = getString "Reports.InCloudDomain"
          OnPremiseDomain = getString "Reports.OnPremiseDomain"
          CompanyPublicIPs = getStringArray "Reports.CompanyPublicIPs" } }