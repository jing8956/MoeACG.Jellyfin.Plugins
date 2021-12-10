module Nullable

open System

let inline hasValue (value: Nullable<_>) = value.HasValue
let inline map mapping (value: Nullable<'a>) =
    if value.HasValue
    then Unchecked.defaultof<'b>
    else mapping (value.Value)
