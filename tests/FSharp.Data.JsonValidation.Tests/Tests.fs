module FSharp.Data.JsonValidation.Tests

open FSharp.Data
open FSharp.Data.JsonValidation
open NUnit.Framework

let valid = function
  | Valid _ -> ()
  | result -> Assert.Fail (sprintf "Expected valid, got %A" result)

let invalid = function
  | Invalid _ -> ()
  | result -> Assert.Fail (sprintf "Expected invalid, got %A" result)

[<Test>]
let ``the docs example works as written (a fat test, but bad code in docs is annoying)`` () =
   let schema = ExactlyOneOf [JsonValue.String "hi"; JsonValue.Number 42M]
   let value = JsonValue.String "goodbye"

   Assert.AreEqual("Invalid \"Expected value to be one of [\"hi\"; 42] but was \"goodbye\"\"", sprintf "%A" (validate schema value))

   valid <| validate schema (JsonValue.Number 42M)

[<Test>]
let ``the tutorial example works as written (a fat test, but bad code in docs is annoying)`` () =
  let someNumbers = ArrayWhose [ItemsMatch AnyNumber]

  let looksLikeAnEmail (value: string) =
    value.Contains "@" && value.Contains "." && value.Length > 3


  let person =
    ObjectWhere
      [
        "name" .= StringThat [IsNotEmpty]
        "favoriteNumbers" .= someNumbers
        "email" .?= StringThat [MeetsCriteria ("be an email", looksLikeAnEmail)]
      ]

  let okayPerson =
    """
    {
      "name": "Saul Goodman",
      "favoriteNumbers": [747, 737000]
    }
    """

  valid <| validate person (JsonValue.Parse okayPerson)

  let emptyNamePerson =
    """
    {
      "name": "",
      "favoriteNumbers": [],
      "email": "mysterious@haunted.house"
    }
    """

  Assert.AreEqual("Invalid \".name Expected string to not be empty\"",  sprintf "%A" <| validate person (JsonValue.Parse emptyNamePerson))


  let badEmail =
    """
    {
      "name": "Jimmy M.",
      "favoriteNumbers": [],
      "email": "not-really-an-email"
    }
    """

  Assert.AreEqual("Invalid \".email Expected string \"not-really-an-email\" to be an email\"", sprintf "%A" <| validate person (JsonValue.Parse badEmail))

[<Test>]
let ``Anything matches anything`` () =
  valid <| validate Anything (JsonValue.String "hi")
  valid <| validate Anything (JsonValue.Number 42M)
  valid <| validate Anything (JsonValue.Array [||])
  valid <| validate Anything (JsonValue.Record [||])

[<Test>]
let ``Exactly only matches exactly what it's given`` () =
  valid <| validate (Exactly <| JsonValue.String "hi") (JsonValue.String "hi")
  invalid <| validate (Exactly <| JsonValue.String "hi") (JsonValue.String "hi2")
  invalid <| validate (Exactly <| JsonValue.String "hi") JsonValue.Null

[<Test>]
let ``ExactlyOneOf matches any of the values it's given`` () =
  let schema = ExactlyOneOf [JsonValue.String "hi"; JsonValue.Number 42M]

  valid <| validate schema (JsonValue.String "hi")
  valid <| validate schema (JsonValue.Number 42M)
  invalid <| validate schema JsonValue.Null
  invalid <| validate schema (JsonValue.String "hi2")

[<Test>]
let ``a JSON number is AnyNumber`` () =
  valid <| validate AnyNumber (JsonValue.Number 42M)

[<Test>]
let ``something other than a JSON number is not AnyNumber`` () =
  invalid <| validate AnyNumber (JsonValue.String "Foo")

[<Test>]
let ``a JSON string is AnyString`` () =
  valid <| validate AnyString (JsonValue.String "Hi")

[<Test>]
let ``something other than a JSON string is not AnyString`` () =
  invalid <| validate AnyString (JsonValue.Number 42M)

[<Test>]
let ``StringThat [IsNotEmpty] matches non-empty strings`` () =
  valid <| validate (StringThat [IsNotEmpty]) (JsonValue.String "hi")
  invalid <| validate (StringThat [IsNotEmpty]) (JsonValue.String "")

[<Test>]
let ``ObjectWhere [...] must have required keys that match the given schema`` () =
  let schema = ObjectWhere [ "foo" .= AnyString ]

  invalid <| validate schema (JsonValue.Parse """ { "foo": 42 } """)
  valid <| validate schema (JsonValue.Parse """ { "foo": "bar" } """)

[<Test>]
let ``ObjectWhere [...] can omit optional keys`` () =
  let schema = ObjectWhere [ "foo" .?= AnyString ]

  valid <| validate schema (JsonValue.Parse """ { } """)
  valid <| validate schema (JsonValue.Parse """ { "foo": "bar" } """)

[<Test>]
let ``ObjectWhere [...] must meet the value schema when optional keys are given`` () =
  let schema = ObjectWhere [ "foo" .?= AnyString ]

  invalid <| validate schema (JsonValue.Parse """ { "foo": 42 } """)


[<Test>]
let ``ObjectWhere [...] ignores keys that aren't mentioned in the schema`` () =
  let schema = ObjectWhere []

  valid <| validate schema (JsonValue.Parse """ { "foo": 42 } """)


[<Test>]
let ``ArrayWhose [LengthIsAtLeast n] works`` () =
  let schema = ArrayWhose [LengthIsAtLeast 4]

  invalid <| validate schema (JsonValue.Array <| Array.replicate 3 JsonValue.Null)
  valid <| validate schema (JsonValue.Array <| Array.replicate 4 JsonValue.Null)
  valid <| validate schema (JsonValue.Array <| Array.replicate 5 JsonValue.Null)

[<Test>]
let ``ArrayWhose [LengthIsAtMost n] works`` () =
  let schema = ArrayWhose [LengthIsAtMost 4]

  valid <| validate schema (JsonValue.Array <| Array.replicate 3 JsonValue.Null)
  valid <| validate schema (JsonValue.Array <| Array.replicate 4 JsonValue.Null)
  invalid <| validate schema (JsonValue.Array <| Array.replicate 5 JsonValue.Null)

[<Test>]
let ``ArrayWhose [LengthIsExactly n] works`` () =
  let schema = ArrayWhose [LengthIsExactly 4]

  invalid <| validate schema (JsonValue.Array <| Array.replicate 3 JsonValue.Null)
  valid <| validate schema (JsonValue.Array <| Array.replicate 4 JsonValue.Null)
  invalid <| validate schema (JsonValue.Array <| Array.replicate 5 JsonValue.Null)


[<Test>]
let ``ArrayWhose [ItemsMatch schema] ensures items all match`` () =
  let schema = ArrayWhose [ItemsMatch AnyNumber]

  invalid <| validate schema (JsonValue.Array [|JsonValue.Null|])
  invalid <| validate schema (JsonValue.Array [|JsonValue.Number 42M; JsonValue.Null|])
  valid <| validate schema (JsonValue.Array [|JsonValue.Number 42M; JsonValue.Number 0M|])

[<Test>]
let ``Either [...] ensures any item matches`` () =
  let schema = Either [Exactly <| JsonValue.Number 42M; AnyString]

  valid <| validate schema (JsonValue.String "42")
  invalid <| validate schema (JsonValue.Number 99M)
  valid <| validate schema (JsonValue.Number 42M)

[<Test>]
let ``Delay matches allowing recursive data structures`` () =
  let rec memory =
    ObjectWhere
      [
        "subject" .= StringThat [IsNotEmpty]
        "linkedMemory" .?= Delay (fun () -> memory)
      ]

  valid <| validate memory (JsonValue.Parse """ { "subject": "Lone Memory" } """)

  """
  {
    "subject": "I link",
    "linkedMemory": {
      "subject": "I'm linked to"
    }
  }
  """
    |> JsonValue.Parse
    |> validate memory
    |> valid

  """
  {
    "subject": "I link",
    "linkedMemory": {
      "subject": 42
    }
  }
  """
    |> JsonValue.Parse
    |> validate memory
    |> invalid