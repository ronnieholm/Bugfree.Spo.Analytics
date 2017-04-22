module Bugfree.Spo.Analytics.Cli.Tests.UtilsTests

open System
open Xunit
open Swensen.Unquote
open Bugfree.Spo.Analytics.Cli.Utils

[<Fact>]
let parseCase1XForwardedForHeader () =
    test <@ parseOriginatingIPFromXForwardedForHeader "12.123.123.90:57505, 12.123.123.90, 12.123.123.90:0" = "12.123.123.90" @>

[<Fact>]
let parseCase2XForwardedForHeader () =
    test <@ parseOriginatingIPFromXForwardedForHeader "12.123.123.90:57505, 12.123.123.90:57505" = "12.123.123.90" @>

[<Fact>]
let parseCase3XForwardedForHeader () =    
    test <@ parseOriginatingIPFromXForwardedForHeader "234.234.234.234, 12.123.123.123:44948, 12.123.123.123:44948" = "234.234.234.234" @>

[<Fact>]
let parseInvalidHeader () =
    raises<Exception> <@ parseOriginatingIPFromXForwardedForHeader "234.234.234.234, 12.123.123.123, 12.123.123.123:44948" @>
