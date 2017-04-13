#load "node_modules/alexa-fs/alexa.fsx"
open Alexa

type State =
  | NotStarted
  | Guessing of target:int * guesses:int

let between1and100 () = System.Random().Next(1, 100)

let startNewGame () =
    Response.say (Text "Try and guess the number I\'m thinking of. It\'s between 1 and 100."),
    Guessing (between1and100(), 0)

let tryGetGuess slots =
  match slots |> Map.tryFind "Guess" with
  | None -> None
  | Some value ->
    let success, guess = System.Int32.TryParse value
    if success then Some guess
    else None

let notUnderstoodResponse = Response.say (Text "Sorry, I didn't understand, try saying a number between 1 and 100")

let handler = Lambda.ofHandler (NotStarted, fun request session -> async {
  match request with
  | Launch ->
    return startNewGame()
  | Intent (name, slots) ->
    match name with
    | "GuessNumber" ->
      let target, guesses =
        match session.Attributes with
        | NotStarted -> between1and100(), 0
        | Guessing(target, guesses) -> target, guesses

      match slots |> tryGetGuess with
      | None ->
        return notUnderstoodResponse, session.Attributes
      | Some guess ->
        if guess = target then
          return
            Response.say (Text (sprintf "Is it %i? Yes! Congratulations, you guessed it in %i tries. Would you like to play again?" guess (guesses + 1))),
            NotStarted
        else if guess < target then
          return
            Response.say (Text (sprintf "Is it %i? Nope, too low, guess again" guess)),
            Guessing (target, guesses + 1)
        else
          return
            Response.say (Text (sprintf "Is it %i? Nope, too high, guess again" guess)),
            Guessing (target, guesses + 1)
    | "AMAZON.YesIntent" ->
      match session.Attributes with
      | NotStarted ->
        return startNewGame()
      | _ ->
        return notUnderstoodResponse, session.Attributes
    | "AMAZON.NoIntent" ->
      match session.Attributes with
      | NotStarted ->
        return Response.exit, NotStarted
      | _ ->
        return notUnderstoodResponse, session.Attributes
    | "AMAZON.StopIntent" ->
      return Response.exit, NotStarted
    | "AMAZON.HelpIntent" ->
      return
        Response.say (Text "The aim of the game is to guess the number I'm thinking of. Try saying a number between 1 and 100, or say \"Stop\" to exit."),
        session.Attributes
    | _ ->
      return notUnderstoodResponse, session.Attributes
  | SessionEnded reason -> return Response.empty, NotStarted
})
