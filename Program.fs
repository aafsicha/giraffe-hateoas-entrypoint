module GiraffeHATEOASEntryPoint.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe


// ---------------------------------
// Models
// ---------------------------------

type AdultEndpoint = AdultEndpoint of string
type ChildEndpoint = ChildEndpoint of string
type AuthorizationEndpoint = AuthorizationEndpoint of string

type Environment =
    | Local
    | Qualification
    | Demonstration
    | Production

type Endpoints = {
    Adults : AdultEndpoint option 
    Childs : ChildEndpoint option
    Authorization : AuthorizationEndpoint
}

type APIDescription = (Environment * Endpoints) list



[<CLIMutable>]
type Adult =
    {
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Age        : int
    }
    override this.ToString() =
        sprintf "%s %s"
            this.FirstName
            this.LastName

    member this.HasErrors() =
        if this.Age < 18 then Some "Person must be an adult (age >= 18)."
        else if this.Age > 150 then Some "Person must be a human being."
        else None
    
    interface IModelValidation<Adult> with
        member this.Validate() =
            match this.HasErrors() with
            | Some msg -> Error (RequestErrors.BAD_REQUEST msg)
            | None     -> Ok this

module Dto =
    type EndpointDto = {
        Name : string
        Url : string
    }
    type EnvironmentsDto = {
        Environment : string
        Apis : EndpointDto list 
    }
    type APIDescriptionDto = EnvironmentsDto list

// ---------------------------------
// Web app
// ---------------------------------
module WebApp =
    let parsingErrorHandler err = RequestErrors.BAD_REQUEST err

    let OptionAdultApiToString (endpoint)  =
        match endpoint with
        | None -> ""
        | Some (AdultEndpoint e) -> e |> string

    let OptionChildApiToString (endpoint)  =
        match endpoint with
        | None -> ""
        | Some (ChildEndpoint e) -> e |> string

    let endpointToListOfEndpoint (endpoint: Endpoints) : Dto.EndpointDto list =
        [
            (endpoint.Adults |> (fun x -> { Name = "adults"; Url = x |> OptionAdultApiToString}));
            (endpoint.Childs|> (fun x -> { Name = "children"; Url = x |> OptionChildApiToString}));
            (endpoint.Authorization |> (fun (AuthorizationEndpoint x) -> { Name = "auth"; Url = x |> string}));
        ]

    let mapApiDescriptionToDto(api : (Environment * Endpoints) list) : Dto.APIDescriptionDto =
        api
        |> List.map (fun (env, detail) -> { Environment = env |> string; Apis = endpointToListOfEndpoint detail})
    
    let apiDescription : APIDescription =
        [ ( Local, { 
            Adults =  (Some (AdultEndpoint "https://localhost:5001/adult" )); 
            Childs = ("https://localhost:5001/child" |> ChildEndpoint |> Some); 
            Authorization = ("https://localhost:5001/auth" |> AuthorizationEndpoint ); 
            });
        ( Qualification, { 
            Adults =  None; 
            Childs = None; 
            Authorization = ("https://localhost:5001/auth" |> AuthorizationEndpoint ); 
            })]
    
    let webApp =
        choose [
            POST >=>
                choose [
                    route "/person" >=> bindJson<Adult> (validateModel Successful.OK)
                ]
            GET >=>
                choose [
                    route "/entrypoint" >=> (mapApiDescriptionToDto apiDescription |> Successful.OK)
                ]
            setStatusCode 404 >=> text "Route not Found" ]

    // ---------------------------------
    // Error handler
    // ---------------------------------

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    // ---------------------------------
    // Config and Main
    // ---------------------------------

    let configureCors (builder : CorsPolicyBuilder) =
        builder
            .WithOrigins(
                "http://localhost:5000",
                "https://localhost:5001")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp)

    let configureServices (services : IServiceCollection) =
        services.AddCors()    |> ignore
        services.AddGiraffe() |> ignore

    let configureLogging (builder : ILoggingBuilder) =
        builder.AddConsole()
               .AddDebug() |> ignore

    [<EntryPoint>]
    let main args =
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot     = Path.Combine(contentRoot, "WebRoot")
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .UseContentRoot(contentRoot)
                        .UseWebRoot(webRoot)
                        .Configure(Action<IApplicationBuilder> configureApp)
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        |> ignore)
            .Build()
            .Run()
        0