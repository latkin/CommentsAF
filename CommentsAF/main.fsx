#if VS
module CommentsAF
#else
#load "./prelude.fsx"

#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#endif

open System
open System.Collections.Generic
open System.Linq
open System.Net
open System.Net.Http
open Microsoft.Azure.WebJobs.Host
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage
open Newtonsoft.Json

type Settings =
    { StorageConnectionString : string
      TableName : string } with
    
    static member load () = 
        { StorageConnectionString =
            Environment.GetEnvironmentVariable("APPSETTING_comments_connectionstring", EnvironmentVariableTarget.Process)
          TableName =
            Environment.GetEnvironmentVariable("APPSETTING_comments_tablename", EnvironmentVariableTarget.Process) }

type CommentRow() =
    inherit TableEntity()
    member val Name = "" with get,set
    member val Comment = "" with get,set

type Comment =
    { time : DateTimeOffset
      name : string
      comment : string } with
    
    static member fromRow (row: CommentRow) =
        { time = row.Timestamp
          name = row.Name
          comment = row.Comment }

let Run(req: HttpRequestMessage, log: TraceWriter) =
    async {
    try
        let postId =
            req.GetQueryNameValuePairs()
            |> Seq.tryFind (fun q -> q.Key = "postid")
    
        match postId with
        | None -> return req.CreateErrorResponse(HttpStatusCode.BadRequest, "postid parameter required")
        | Some(postId) ->
            let settings = Settings.load()
            let postKey = sprintf "post-%s" postId.Value
            let table = 
                CloudStorageAccount.Parse(settings.StorageConnectionString)
                    .CreateCloudTableClient()
                    .GetTableReference(settings.TableName)
            let q =
                TableQuery<CommentRow>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, postKey))
    
            let rows = table.ExecuteQuery(q) |> Seq.map Comment.fromRow
            let resp = req.CreateResponse(HttpStatusCode.OK)
            resp.Content <- new StringContent(JsonConvert.SerializeObject(rows.ToArray()))
    
            return resp
    with
    | exn ->
        log.Error("Unknown error getting comments", exn)
        return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Unknown error")
    } |> Async.RunSynchronously
