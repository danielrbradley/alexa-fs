#r "node_modules/fable-core/Fable.Core.dll"

open Fable.Core
open Fable.Core.JsInterop

[<Pojo>]
type LambdaContext =
  {
    callbackWaitsForEmptyEventLoop : bool
    logGroupName : string
    logStreamName : string
    functionName : string
    memoryLimitInMB : string
    functionVersion : string
    invokeid : string
    awsRequestId : string
  }

type NativeHandler<'Request, 'Response> = System.Func<'Request, LambdaContext, (System.Func<exn option, 'Response option, unit>), unit>

type AsyncHandler<'Request, 'Response> = LambdaContext -> 'Request -> Async<'Response>

type AsyncPipe<'Request, 'Response> = AsyncHandler<'Request, 'Response> -> LambdaContext -> 'Request -> Async<'Response>

let withMiddleware (handler : AsyncHandler<'Request, 'Response>) (pipes : AsyncPipe<'Request, 'Response> list)
  : AsyncHandler<'Request, 'Response> =
    let rec processNext (remainingHandlers : AsyncPipe<'Request, 'Response> list) =
      fun context request -> async {
        match remainingHandlers with
        | [] -> return! handler context request
        | next :: rest ->
          return! next (processNext rest) context request
      }
    processNext pipes

let handler (handler : AsyncHandler<'Request, 'Response>) : NativeHandler<'Request, 'Response> =
  System.Func<_,_,_,_>(fun request context callback ->
    async {
      try
        let! response = handler context request
        callback.Invoke(None, Some response)
      with ex ->
        callback.Invoke(Some ex, None)
    } |> Async.StartImmediate)
