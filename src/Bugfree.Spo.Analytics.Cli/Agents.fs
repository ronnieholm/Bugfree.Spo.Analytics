﻿module Bugfree.Spo.Analytics.Cli.Agents

open System
open Domain

// Examples of agent programming:
//   https://fsharpforfunandprofit.com/posts/concurrency-actor-model/
//   https://msdn.microsoft.com/en-us/library/hh297112(v=vs.100).aspx

let settings = Configuration.getSettings()

type Agent<'T> = MailboxProcessor<'T>

type LoggerMessage =
    | Message of string
    | Retrieve of AsyncReplyChannel<string array>

// Strictly speaking, we don't need the logger agent anymore and could
// print line directly. Earlier code contained multiple processing agents
// running in parallel, each sending log message to the single logger event.
let logger = Agent<LoggerMessage>.Start (fun inbox ->
    let sizeOfCircularBuffer = 100
    let circularLog = Array.create sizeOfCircularBuffer ""
    let mutable logPosition = 0

    let rec messageLoop() = async {
        let! message = inbox.Receive()
        match message with
        | Message m ->
            let s = sprintf "%s %s" (DateTime.Now.ToUniversalTime().ToString()) m
            printfn "%s" s
            circularLog.[logPosition % sizeOfCircularBuffer] <- s
            logPosition <- logPosition + 1
            return! messageLoop()
        | Retrieve channel ->
            let entries = 
                [|logPosition..(logPosition + sizeOfCircularBuffer - 1)|]              
                |> Array.map (fun i -> sprintf "%s" circularLog.[i % sizeOfCircularBuffer])
            channel.Reply entries
            return! messageLoop()
    }
    messageLoop())

type VisitorMessage =
    | Visit of Visit

let visitor = Agent<VisitorMessage>.Start (fun inbox ->   
    let rec messageLoop (settings: Configuration.Settings) visits = async {
        let! message = inbox.Receive()
        
        // Each page visit results in up to two message: one the page DOM is
        // ready and another when the page is loaded. In most cases, the DOM
        // ready message arrives before the DOM load message, but network 
        // issues and/or the async nature of JavaScript executing in the browser
        // mean we should prepare for any ordering. Both messages are of the same 
        // type, holding a correlation id. With the correlation id, we can amend
        // already saved messages with timing data.
        match message with
        | Visit visit ->
            let visits' = visit :: visits
            let length = visits' |> List.length
            logger.Post (Message (sprintf "Queued message %d with CorrelationId %s" length (visit.CorrelationId.ToString())))

            if length >= (settings.VisitorAgent.CommitThreshold) then
                try
                    logger.Post (Message (sprintf "About to persist %d messages" length))
                    let mostRecentFirst = List.rev visits'
                    match Database.save (settings.DatabaseConnectionString) mostRecentFirst with
                    | Choice1Of2 rows -> 
                        logger.Post (Message (sprintf "Persisted %d messages as %d unique visits" length rows))
                        return! messageLoop settings []
                    | Choice2Of2 exn -> 
                        logger.Post (Message (sprintf "Exception occurred: %s %s %A" exn.Message exn.StackTrace (mostRecentFirst |> List.head)))
                        // Assume intermittend database failure and retain visits, causing
                        // another save attempt on the next message arrival because the queue
                        // length is still >= CommitThreshold. In case the failure is more 
                        // permanent, queue processing is unable to move beyond the failing
                        // message. If this turns out to be an issue, we should introduce a
                        // dead letter queue or drop the faulting message after a number of 
                        // retries.
                        return! messageLoop settings visits'
                with
                | e -> 
                    logger.Post (Message (sprintf "Exception during message processing: %A" e))
            else
                return! messageLoop settings visits'
    }
    
    messageLoop settings [])