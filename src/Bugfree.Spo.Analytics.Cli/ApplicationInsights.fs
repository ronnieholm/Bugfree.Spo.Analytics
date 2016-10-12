module Bugfree.Spo.Analytics.Cli.ApplicationInsights

open System
open System.Configuration
open System.Diagnostics
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open Microsoft.ApplicationInsights.Extensibility
open Microsoft.ApplicationInsights.Extensibility.Implementation
open Suave

// Originally based on https://github.com/isaacabraham/azure-fsharp-helpers

let client = TelemetryClient()

let private buildOperationName (url: Uri) =
    if url.AbsolutePath.StartsWith "/api/" && url.Segments.Length > 2 
    then "/api/" + url.Segments.[2]
    else url.AbsolutePath

let withRequestTracking (webPart: WebPart) ctx =
    let operation = client.StartOperation<RequestTelemetry>(buildOperationName ctx.request.url)
    
    async {                
        try
            try
                // Execute webpart
                let! ctx' = webPart ctx
            
                // Map properties of result into ApplicationInsights
                ctx'
                |> Option.iter (fun ctx ->
                    operation.Telemetry.ResponseCode <- ctx.response.status.code.ToString()
                    operation.Telemetry.Success <- Nullable (int ctx.response.status.code < 400))            
                return ctx'
            with ex ->
                // Log error and rethrow
                operation.Telemetry.ResponseCode <- "500"
                operation.Telemetry.Success <- Nullable false            
                ExceptionTelemetry(ex, HandledAt = ExceptionHandledAt.Unhandled) |> client.TrackException
                raise ex
                return None
        finally
            operation.Telemetry.Url <- ctx.request.url
            operation.Telemetry.HttpMethod <- ctx.request.``method``.ToString()
            client.StopOperation operation 
    }

do
    let settings = Configuration.getSettings()
    TelemetryConfiguration.Active.TelemetryChannel.DeveloperMode <- Nullable true
    TelemetryConfiguration.Active.InstrumentationKey <- settings.ApplicationInsights.InstrumentationKey
