#if VS
module run
#else
#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#load "commentstorage.fs"
#load "processing.fs"
#endif

open System
open CommentsAF
open CommentsAF.CommentStorage
open System.Collections.Generic
open System.Net
open System.Net.Http
open Microsoft.Azure.WebJobs.Host
open Newtonsoft.Json

module Option =
    let getOrElse x = function None -> x | Some(v) -> v

let (|AddComments|Error|) (req: HttpRequestMessage) =
    if req.Method = HttpMethod.Post then
        AddComments(async {
            let! content = req.Content.ReadAsStringAsync() |> Async.AwaitTask
            try
                let newComment = JsonConvert.DeserializeObject<AdminComment[]>(content)
                return Some(newComment)
            with
            | :? JsonReaderException -> return None
        })
    else Error(sprintf "unsupported method %s" req.Method.Method)

let Run(req: HttpRequestMessage, log: TraceWriter) =
    async {
    try
        match req with
        | AddComments(newCommentsOpt) ->
            let! newCommentsOpt = newCommentsOpt
            match newCommentsOpt with
            | Some(newComments) ->
                log.Info(sprintf "Admin request to add %d comments" newComments.Length)
                let settings = Settings.load()
                let storage = CommentStorage(settings)
                newComments
                |> Array.iter (fun newComment ->
                    newComment
                    |> Processing.adminCommentToPending log req settings
                    |> storage.AddCommentForPost
                    |> ignore

                    log.Info(sprintf "Successfully added new comment to post %s" newComment.postid)
                )

                return req.CreateResponse(HttpStatusCode.OK)
            | None ->
                log.Error(sprintf "Request to add malformed comment")
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "malformed comment")
        | Error(msg) ->
            log.Error(sprintf "Error: %s" msg)
            return req.CreateErrorResponse(HttpStatusCode.BadRequest, msg)
    with
    | ProcessingExn(msg) ->
        log.Error(sprintf "Processing error: %s" msg)
        return req.CreateErrorResponse(HttpStatusCode.BadRequest, msg)
    | exn ->
        log.Error("Unknown error", exn)
        return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Unknown error")
    } |> Async.RunSynchronously
