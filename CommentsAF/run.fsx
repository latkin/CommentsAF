#if VS
module run
#else
#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
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

let (|GetComments|AddComment|Error|) (req: HttpRequestMessage) =
    if req.Method = HttpMethod.Get then
        req.GetQueryNameValuePairs()
        |> Seq.tryFind (fun q -> q.Key = "postid")
        |> Option.map (fun kvp -> GetComments(kvp.Value))
        |> Option.getOrElse (Error("postid parameter required"))
    elif req.Method = HttpMethod.Post then
        AddComment(async {
            let! content = req.Content.ReadAsStringAsync() |> Async.AwaitTask
            try
                let newComment = JsonConvert.DeserializeObject<UserComment>(content)
                if String.IsNullOrWhiteSpace(newComment.comment) ||
                   String.IsNullOrWhiteSpace(newComment.name) ||
                   String.IsNullOrWhiteSpace(newComment.postid) then
                    return None
                else
                    return Some(newComment)
            with
            | :? JsonReaderException -> return None
        })
    else Error(sprintf "unsupported method %s" req.Method.Method)

let Run(req: HttpRequestMessage, log: TraceWriter) =
    async {
    try
        match req with
        | GetComments(postId) ->
            log.Info(sprintf "Request to get comments for post %s" postId)

            let storage = CommentStorage(StorageSettings.load())
            let comments = storage.GetCommentsForPost(postId)

            log.Info(sprintf "Loaded %d comments for post %s" comments.Length postId)

            let resp = req.CreateResponse(HttpStatusCode.OK)
            resp.Content <- new StringContent(JsonConvert.SerializeObject(comments))
            return resp
        | AddComment(newCommentOpt) ->
            let! newCommentOpt = newCommentOpt
            match newCommentOpt with
            | Some(newComment) ->
                log.Info(sprintf "Request to add comment for post %s" newComment.postid)
                let storage = CommentStorage(StorageSettings.load())
                let finalComment =
                    newComment
                    |> Processing.userCommentToPending
                    |> storage.AddCommentForPost

                log.Info(sprintf "Successfully added new comment to post %s" newComment.postid)

                let resp = req.CreateResponse(HttpStatusCode.OK)
                resp.Content <- new StringContent(JsonConvert.SerializeObject(finalComment))
                return resp
            | None ->
                log.Error(sprintf "Request to add malformed comment")
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "malformed comment")
        | Error(msg) ->
            log.Error(sprintf "Error: %s" msg)
            return req.CreateErrorResponse(HttpStatusCode.BadRequest, msg)
    with
    | exn ->
        log.Error("Unknown error", exn)
        return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Unknown error")
    } |> Async.RunSynchronously
