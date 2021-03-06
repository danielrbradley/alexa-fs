# Getting Started

Install package from GitHub

```
npm install --save git://github.com/danielrbradley/alexa-fs.git
```

_While in early beta phase, I haven't yet set this up on NPM_

## Jump to:
- [Introduction](#introduction)
  - [Hello World Example](#hello-world-example)
- [Models Reference](#models-reference)
  - [Requests](#requests)
  - [Response](#response)
  - [Session](#session)
  - [Handler](#handler)
- [Background](#background)

# Introduction

`alexa-fs` is a lightweight library for writing Alexa skills in F# which are then compilled to JS via [Fable](http://fable.io/).

See the [examples folder](examples) for more complete sample skills.

## Hello World Example

```fsharp
#load "node_modules/alexa-fs/alexa.fsx"
open Alexa

let handler = lambda (None, fun request session -> async {
  return (Response.say (Text "Hello world!")), None
})
```

Breaking it down step by step:

1. Load the alexa-fs library and open the root module so we don't have to write `Alexa.` everywhere.

    ```fsharp
    #load "alexa.fsx"
    open Alexa
    ```

2. Define the lambda entry point.

    ```fsharp
    let handler = ...
    ```

    This is a public variable, so Fable will export this function so you can configure the lambda handler as `index.handler`.

3. Slightly more to bite off this time - create an AWS lambda compatible handler function.

    ```fsharp
    let handler = lambda (None, fun request session -> async {
      ...
    })
    ```

    1. In javascript the handler function looks something like:

        ```js
        var handler = function(event, context, callback) {
          doSomethingAsync(event, function(result) {
            callback(null, result);
          });
        };
        ```

        Native JS callbacks are not pretty to write in F# so the `Lambda.ofHandler` helper replaces callbacks with F# `async` instead.

    2. The first argument defines the initial attributes for when the session begins.

        In this app, we have no need for state, so we just set it to `Option.None`. _See the section on [session attibutes](#session-attributes) for more information._

    3. The second argument is the actual handler function. This takes two arguments: request and session and returns a result asynchronously.
    
        - The request describes the action the user has taken (e.g. saying something). _See section on [Requests](#requests) for more info._
        - The session object is general information about the overall session including the current session attributes, session identifier and user credentials.

4. Return a response.

    ```fsharp
    return (Response.say (Text "Hello world!")), None
    ```

    The return type of a handler is a tupe where the first item is the response for the user and the second is the new attributes (which here we use the placeholder `Option.None`).

    _See section on [Responses](#response) for more info._

# Models Reference

## Requests

There are three possible request types: Launch, Intent and Session Ended. The request type models these as a Discriminated Union.

```fsharp
type Request =
  | Launch
  | Intent of name : string * Slots
  | SessionEnded of SessionEndedReason
```

### Launch Request

A launch request has no arguments. This is triggered when a user says something like "Alexa start _Your Invocation Name_".

### Intent Request

Intents the possible things the user can say that your app can respond to. An intent has name which is just a string, but can also have option "slots" which are essentially just arguments.

```fsharp
type Slots = Map<string, string>
```

### Session Ended Request

There can be three possible reasons for a session to be ended: when the user explicitly ends a session, when an error occurrs (e.g. a badly formatted response), or when a user doesn't response to the reprompts. Any response to a session ended request will not be delivered to the user.

```fsharp
type SessionEndedReason =
  | UserInitiated
  | Error
  | ExceededMaxReprompts
```

## Response

Responses can contain 5 pieces of information:
- `Speech`: What Alexa should say.
- `Reprompt`: What Alexa should say if the user doesn't respond.
- `EndSession`: Set to true to end the conversation.
- `Card`: Display a card in the Alexa companion app.

```fsharp
type Response =
  {
    EndSession : bool
    Speech : Speech option
    Reprompt : Speech option
    Card : Card option
  }
```

All these arguments are optional, though at least one of them should normally be specified otherwise Alexa will just sit in silence waiting for the user to say something (untill it times-out).

## Session

In addition to the what the user has requested additional information is available about the user's session.

- `ApplicationId`: a unique identifier of your skill which should be verified to ensure that another skill is not accessing your handler.
- `UserId`: a unique identifier for the user which is generated at the point they enable your skill (and is changed if they disable and re-enable your skill).
- `UserAccessToken`: if using account linking, this is the token returned by the log-in process.
- `SessionId`: a unique identifier to correlate requests within a single session.
- `IsNew`: indicates if this is the first request of the session.
- `Attributes`: used to maintain state for the duration of a session (see [attributes](#attributes) for more info).

```fsharp
type Session<'a> =
  {
    ApplicationId : string
    UserId : string
    UserAccessToken : string option
    SessionId : string
    IsNew : bool
    Attributes : 'a
    Raw : Interop.Request
  }
```

### Attributes

State is important for almost all skills to maintain session variables during the course of a conversation, much like cookies in a web browser. These attributes are just a JSON object which is included in responses and will get passed back to the lambda with the next response from the user. When a conversation ends, the attributes are discarded and the next session will start with no attributes again.

This type will need to be able to be converted to and from a simple javascript object.

## Handler

The top level handler function is given a request and a session object and must return a response along with new attributes asynchronously.

```fsharp
type Handler<'a> = Request -> Session<'a> -> Async<Response * 'a>
```

The type parameter of the session is used to indicate the type to represent the session attributes. The compiller will then also ensure that the attributes in the response are also of the same type.

### Interop

To make your handler into a function which is compatible with the required AWS Lambda signature, it must be passed into the `lambda` bootstrapper function. Along with the handler, a default value for the specified attributes type must also be given so that when a request is the first in a session, the handler will be passed these specified attributes instead.

```fsharp
let lambda (defaultAttributes : 'a, handler : Handler<'a>) :
  Lambda.NativeHandler<Interop.Request, Interop.Response option> =
```

# Background

## Why use F#?
The first skill I wrote was written using the official aws-sdk, which I found to have some strange abstractions and not very good documentation.

I then wrote the [alexa-ts](https://github.com/o2Labs/alexa-ts) library as an alternative which can be used from TypeScript or javascript with the aim of having minimal abstractions over the underlying request/response models as well as good type checking to prevent various bugs.

However, the type inference in TypeScript lacks in some areas: it's quite verbose like C# and doesn't have reliable inference leading to needing lots of hints and annotations. Untimately, I found myself missing F#'s liteweight syntax and powerful type checking!

## Why compile to JS?
After creating a POC using dotnet core, I was finding the initial startup times of the lambda to be too long causing the skill to time-out, however using JS in lambdas doesn't seem to suffer from the same issue.
