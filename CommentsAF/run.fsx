#if VS
module run
#else
#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#load "commentstorage.fs"
#endif

open CommentsAF.CommentStorage
open System.Collections.Generic
open System.Net
open System.Net.Http
open Microsoft.Azure.WebJobs.Host
open Newtonsoft.Json

let Run(req: HttpRequestMessage, log: TraceWriter) =
    async {
    try
        let postIdOpt =
            req.GetQueryNameValuePairs()
            |> Seq.tryFind (fun q -> q.Key = "postid")
            |> Option.map (fun kvp -> kvp.Value)
    
        match postIdOpt with
        | None -> return req.CreateErrorResponse(HttpStatusCode.BadRequest, "postid parameter required")
        | Some(postId) ->
            let storage = CommentStorage(StorageSettings.load())
            let comments = storage.GetCommentsForPost(postId)

            log.Info(sprintf "Loaded %d comments for post %s" comments.Length postId)

            let resp = req.CreateResponse(HttpStatusCode.OK)
            resp.Content <- new StringContent(JsonConvert.SerializeObject(comments))
            return resp
    with
    | exn ->
        log.Error("Unknown error getting comments", exn)
        return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Unknown error")
    } |> Async.RunSynchronously
