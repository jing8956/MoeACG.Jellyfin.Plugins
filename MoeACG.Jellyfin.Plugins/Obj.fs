module Obj

let inline defaultValue value obj = if isNull obj then value else obj
let inline iter action obj = if isNull obj then () else action obj
let inline map mapping obj = if isNull obj then Unchecked.defaultof<'b>  else mapping obj
let inline orElse ifNull obj = defaultValue ifNull obj
