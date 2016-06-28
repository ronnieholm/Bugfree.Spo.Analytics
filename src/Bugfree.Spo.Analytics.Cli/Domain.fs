module Bugfree.Spo.Analytics.Cli.Domain

open System
open System.Net

type Visit =
    { CorrelationId: Guid
      Timestamp: DateTime
      LoginName: string
      Url: string
      PageLoadTime: int option

      // When running in Azure using HttpPlatformHandler, IP is Some, otherwise None
      IP: IPAddress option

      // Some if client identified itself, otherwise None
      UserAgent: string option }