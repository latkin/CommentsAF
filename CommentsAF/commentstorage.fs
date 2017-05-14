namespace CommentsAF

open System
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

type Comment =
    { time : DateTimeOffset
      name : string
      comment : string }

module CommentStorage =
    type StorageSettings =
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

    type CommentStorage(settings) =
        let table = 
                CloudStorageAccount.Parse(settings.StorageConnectionString)
                    .CreateCloudTableClient()
                    .GetTableReference(settings.TableName)

        let commentFromRow (row: CommentRow) =
            { time = row.Timestamp
              name = row.Name
              comment = row.Comment }

        member __.GetCommentsForPost(postId) =
            let postKey = sprintf "post-%s" postId
            let q =
                TableQuery<CommentRow>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, postKey))

            table.ExecuteQuery(q) |> Seq.map commentFromRow |> Array.ofSeq