#r "node_modules/fable-core/Fable.Core.dll"

open Fable.Core
open Fable.Core.JsInterop

module Interop =
  [<Pojo>]
  type User =
    {
      userId : string
      accessToken : string option
    }

  [<Pojo>]
  type Application =
    {
      applicationId : string
    }

  [<Pojo>]
  type Session =
    {
      ``new`` : bool
      sessionId : string
      attributes : obj
      application : Application
      user : User
    }

  [<StringEnum>]
  type RequestType =
    | [<CompiledName("LaunchRequest")>] LaunchRequest
    | [<CompiledName("IntentRequest")>] IntentRequest
    | [<CompiledName("SessionEndedRequest")>] SessionEndedRequest

  [<StringEnum>]
  type SessionEndedReason =
    | [<CompiledName("USER_INITIATED")>] UserInitiated
    | [<CompiledName("ERROR")>] Error
    | [<CompiledName("EXCEEDED_MAX_REPROMPTS")>] ExceededMaxReprompts

  [<Pojo>]
  type Slot =
    {
      name : string
      value : obj
    }

  type Slots =
    [<Emit("$0[$1]{{=$2}}")>]
    abstract Item: string -> Slot with get

  [<Pojo>]
  type Intent =
    {
      name : string
      slots : Slots
    }

  [<Pojo>]
  type RequestBody =
    {
      requestId : string
      timeStamp : string
      ``type`` : RequestType
      reason : SessionEndedReason option
      intent : Intent option
    }

  [<Pojo>]
  type Request =
    {
      version : string
      session : Session
      request : RequestBody
    }

  [<StringEnum>]
  type SpeechType =
    | [<CompiledName("PlainText")>] PlainText
    | [<CompiledName("SSML")>] SSML

  type Speech =
    {
      ``type`` : SpeechType
      text : string option
      ssml : string option
    }

  type Reprompt =
    {
      outputSpeech : Speech
    }

  [<StringEnum>]
  type CardType =
    | [<CompiledName("Simple")>] Simple
    | [<CompiledName("Standard")>] Standard
    | [<CompiledName("LinkAccount")>] LinkAccount

  type Image =
    {
      smallImageUrl : string
      largeImageUrl : string
    }

  type Card =
    {
      ``type`` : CardType
      title : string option
      content : string option
      text : string option
      image : Image option
    }

  type ResponseBody =
    {
      shouldEndSession : bool
      outputSpeech : Speech option
      reprompt : Reprompt option
      card : Card option
    }

  type Response =
    {
      version : string
      sessionAttributes : obj
      response : ResponseBody
    }

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

  type LambdaHandler = (Request * LambdaContext * (System.Func<exn option, Response option, unit>)) -> unit

  type AsyncLambdaHandler = LambdaContext -> Request -> Async<Response>

  let createHandler (handler : AsyncLambdaHandler) : LambdaHandler =
    fun (request, context, callback) ->
      async {
        try
          let! response = handler context request
          if request.request.``type`` = RequestType.SessionEndedRequest then
            callback.Invoke(None, None)
          else
            callback.Invoke(None, Some response)
        with ex ->
          callback.Invoke(Some ex, None)
      } |> Async.Start

open Interop

let handler = createHandler (fun context request -> async {
  let response =
    {
      version = "1.0.0"
      sessionAttributes = None
      response =
        {
          shouldEndSession = true
          outputSpeech = (Some {
            ``type`` = PlainText
            text = Some "Hello world"
            ssml = None
          })
          reprompt = None
          card = None
        }
    }
  return response
})
