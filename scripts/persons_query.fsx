#r "nuget: FSharp.Data, 4.2.4"
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open System.Net

System.Net.ServicePointManager.ServerCertificateValidationCallback <-  (fun _ _ _ _ -> true)

Http.RequestString
    ( "http://localhost:5000/person", 
      headers = [ ContentType HttpContentTypes.Json ],
      body = TextRequest """ {
            "FirstName":"Jean-Michel",
            "MiddleName": 
            { 
                "case": "Some",
                "fields":["Petula"]
            },
            "LastName":"Patulacci",
            "Age":30
        } """)
      |> printf "%s"