#load "../alexa.fsx"
open Alexa

let handler = Lambda.ofHandler (None, fun request session -> async {
  return (Response.say (Text "Hello world!")), None
})
