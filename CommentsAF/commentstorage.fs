namespace CommentsAF

open System
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

/// final form of a comment, as read out of table storage
type Comment =
    { time : DateTimeOffset
      name : string
      comment : string }

/// unsanitized comment as it arrives from the user
type UserComment =
    { postid: string
      name : string
      comment : string
      captcha : string }

/// sanitized comment ready for storage
type PendingComment = 
    { postid: string
      name : string
      comment : string }

type Settings =
    { StorageConnectionString : string
      TableName : string
      ReCaptchaSecret : string } with

    static member load () = 
        { StorageConnectionString =
            Environment.GetEnvironmentVariable("APPSETTING_comments_connectionstring", EnvironmentVariableTarget.Process)
          TableName =
            Environment.GetEnvironmentVariable("APPSETTING_comments_tablename", EnvironmentVariableTarget.Process)
          ReCaptchaSecret =
            Environment.GetEnvironmentVariable("APPSETTING_comments_recaptchasecret", EnvironmentVariableTarget.Process) }

module CommentStorage =
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
            table.ExecuteQuery(q)
            |> Seq.map commentFromRow
            |> Array.ofSeq
            |> Array.sortBy (fun c -> c.time)

        member __.AddCommentForPost(comment: PendingComment) =
            let commentRow = CommentRow(PartitionKey = (sprintf "post-%s" comment.postid),
                                        RowKey = Guid.NewGuid().ToString(),
                                        Name = comment.name,
                                        Comment = comment.comment)
            let result = table.Execute(TableOperation.Insert(commentRow))
            (result.Result :?> CommentRow) |> commentFromRow