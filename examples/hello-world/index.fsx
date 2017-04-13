#load "../../alexa.fsx"

open Alexa

let handler = Lambda.ofHandler (None, fun request session -> async {
  let response =
    Response.say (Text "Hello world!")
    |> Response.withReprompt (SSML """<speech>Use SSML to control how a word is <w role="ivona:VBD">read</w> out.</speech>""")
    |> Response.withCustomCard
      {
        Title = "My Card"
        Content = "Info about this skill"
        Image = Some
          {
            SmallUrl = "https://placehold.it/720x480"
            LargeUrl = "https://placehold.it/1200x800"
          }
      }
    |> Response.endSession

  return response, None
})
