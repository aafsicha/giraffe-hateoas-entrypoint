module GiraffeHATEOASEntryPoint.App

open System
open System.IO
open System.Text.Json
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

type Message =
    {
        Text : string
    }

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


// ---------------------------------
// Web app
// ---------------------------------
module WebApp =
    let parsingErrorHandler err = RequestErrors.BAD_REQUEST err
    
    let webApp =
        choose [
            POST >=>
                choose [
                    route "/person" >=> bindJson<Adult> (validateModel Successful.OK)
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