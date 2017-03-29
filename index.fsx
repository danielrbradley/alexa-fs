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
      } |> Async.StartImmediate

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

type Speech =
  | Text of string
  | SSML of string

type Image =
  {
    SmallUrl : string
    LargeUrl : string
  }

type Card =
  {
    Title : string
    Content : string
    Image : Image option
  }

module Response =
  let private makeResponse sessionAttributes response =
    {
      version = "1.0.0"
      sessionAttributes = createObj [ attributesKey ==> sessionAttributes ]
      response = response
    }

  let private makeSpeech speech : Interop.Speech =
    match speech with
    | Text text ->
      {
        ``type`` = Interop.SpeechType.PlainText
        text = Some text
        ssml = None
      }
    | SSML ssml ->
      {
        ``type`` = Interop.SpeechType.SSML
        text = None
        ssml = Some ssml
      }

  let private makeImage image =
    {
      smallImageUrl = image.SmallUrl
      largeImageUrl = image.LargeUrl
    }

  let private defaultCard =
    {
      ``type`` = Interop.CardType.Simple
      title = None
      content = None
      text = None
      image = None
    }

  let private makeCard card =
    match card.Image with
    | None ->
      { defaultCard with
          ``type`` = Interop.CardType.Simple
          title = Some card.Title
          content = Some card.Content
      }
    | Some image ->
      {
        defaultCard with
          ``type`` = Interop.CardType.Standard
          title = Some card.Title
          image = Some (makeImage image)
          text = Some card.Content
      }

  let private emptyBody =
    {
      shouldEndSession = false
      outputSpeech = None
      reprompt = None
      card = None
    }

  let withSpeech speech response =
    {
      response with
        response =
          {
            response.response with
              outputSpeech = Some (makeSpeech speech)
          }
    }

  let say speech =
    makeResponse None emptyBody
    |> withSpeech speech

  let setState state response =
    makeResponse state (response.response)

  let withReprompt speech response =
    {
      response with
        response =
          {
            response.response with
              reprompt = Some { outputSpeech = (makeSpeech speech) }
          }
    }

  let endSession response =
    {
      response with
        response =
          {
            response.response with
              shouldEndSession = true
          }
    }

  let withCard card response =
    {
      response with
        response =
          {
            response.response with
              card = Some (makeCard card)
          }
    }

  let linkAccount speechOption =
    makeResponse
      None
      {
        emptyBody with
          outputSpeech = speechOption |> Option.map makeSpeech
          card = Some
            {
              defaultCard with
                ``type`` = Interop.CardType.LinkAccount
            }
      }

let handler = createHandler (fun context request -> async {
  let request, session = parseRequest None request
  let response =
    Response.say (Text "Hello world!")
    |> Response.setState None
    |> Response.withCard
      {
        Title = "My Card"
        Content = "Info about this skill"
        Image = Some
          {
            SmallUrl = "https://placehold.it/720x480"
            LargeUrl = "https://placehold.it/1200x800"
          }
      }
    |> Response.withReprompt (SSML """<speech>Use SSML to control how a word is <w role="ivona:VBD">read</w> out.</speech>""")
    |> Response.endSession

  // Prompt user to log in
  let linkAccount = Response.linkAccount (Some (Text "Please log in."))
  return response
})
