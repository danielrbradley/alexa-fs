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
  type Intent =
    {
      name : string
      slots : obj
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

type Slots = Map<string, string>

type Request =
  | Launch
  | Intent of name : string * Slots
  | SessionEnded of SessionEndedReason

type Session<'a> =
  {
    Attributes : 'a
    IsNewSession : bool
    Original : Interop.Request
  }

let attributesKey = "AlexaFs"

[<Emit("Object.keys($0)")>]
let objKeys obj : string[] = jsNative

let parseRequest (initialAttributes : 'a) (request : Interop.Request) : Request * Session<'a> =
  let parsedRequest =
    match request.request.``type`` with
    | RequestType.LaunchRequest -> Launch
    | RequestType.IntentRequest ->
      let intent = request.request.intent.Value
      let slotKeys = objKeys intent.slots
      let slots =
        slotKeys
        |> Array.map(fun key ->
          let slot = intent.slots?(key)
          !!slot?name, !!slot?value
        )
        |> Map.ofArray
      Intent(intent.name, slots)
    | RequestType.SessionEndedRequest -> SessionEnded(request.request.reason.Value)
  let attributes = !!request.session.attributes?(attributesKey)
  let session =
    {
      Attributes = defaultArg attributes initialAttributes
      IsNewSession = request.session.``new``
      Original = request
    }
  parsedRequest, session

let makeResponse sessionAttributes response =
  {
    version = "1.0.0"
    sessionAttributes = createObj [ attributesKey ==> sessionAttributes ]
    response = response
  }

let handler = createHandler (fun context request -> async {
  let request, session = parseRequest None request
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
