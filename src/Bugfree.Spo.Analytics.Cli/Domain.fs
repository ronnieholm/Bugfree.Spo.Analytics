﻿module Bugfree.Spo.Analytics.Cli.Domain

open System
open System.Net

type Visit =
    { CorrelationId: Guid
      Timestamp: DateTime
      LoginName: string
      SiteCollectionUrl: string
      VisitUrl: string
      PageLoadTime: int option
      IP: IPAddress
      // Some if client identified itself, otherwise None
      UserAgent: string option }