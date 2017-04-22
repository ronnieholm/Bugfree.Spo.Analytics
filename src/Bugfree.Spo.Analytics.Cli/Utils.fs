module Bugfree.Spo.Analytics.Cli.Utils

open System.Collections.Generic
open System.Net

let memoize fn =
    let cache = Dictionary<_, _>()
    fun x ->
        let mutable result = Unchecked.defaultof<_>
        let ok = cache.TryGetValue(x, &result)
        if ok then result
        else 
            let result = fn x
            cache.[x] <- result
            result

let parseOriginatingIPFromXForwardedForHeader (xForwardedFor: string) =
    // With Azure's HttpPlatformHandler, the Azure load balancer and/or IIS reverse
    // proxy adds the x-forwarded-for header to the oringal request. It contains the
    // true client's IP. Multiple formats for x-forwarded-for seems to exist:
    //
    // 1. The same IP is listed once without port number and twice with a port number
    // as in "12.345.678.90:57505, 12.345.678.90, 12.345.678.90:0".
    //
    // 2. The same IP and port number is listed multiple times. The IP is in fact the 
    // originating IP. For intance: "123.123.123.123:31204, 123.123.123.123:31204".
    //
    // 3. Multiple IPs are listed and the originating client IP is the one without port
    // number as in "234.234.234.234, 12.123.123.123:44948, 12.123.123.123:44948".
    let ips = xForwardedFor.Split(',') |> Array.map (fun s -> s.Trim())

    // Cases 1 and 2
    let case12Ips = 
        ips
        |> Array.map (fun s ->               
            let lastColon = s.LastIndexOf(':')
            if lastColon <> -1 then s.Substring(0, lastColon) else s)
        |> Array.distinct

    // Case 3
    let case3Ips = ips |> Array.filter (fun s -> s.LastIndexOf(':') = -1)

    if Array.length case12Ips = 1 then case12Ips.[0]
    elif Array.length case3Ips = 1 then case3Ips.[0]
    else failwithf "Unable to parse: %s" xForwardedFor

