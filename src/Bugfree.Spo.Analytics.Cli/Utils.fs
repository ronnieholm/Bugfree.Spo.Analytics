module Bugfree.Spo.Analytics.Cli.Utils

open System.Collections.Generic

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