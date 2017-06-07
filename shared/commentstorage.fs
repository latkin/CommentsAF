namespace CommentsAF

open System
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

/// form of comment that is returned to client webpage
type WebComment =
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
      commentHtml : string
      commentRaw : string 
      createdAt : DateTimeOffset 
      ipAddress : string }

type AdminComment = 
    { postid: string
      name : string
      commentHtml : string
      createdAt : DateTimeOffset
      ipAddress : string }

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
        member val CommentHtml = "" with get,set
        member val CommentRaw = "" with get,set
        member val IpAddress = "" with get,set
        member val CreatedAt = DateTimeOffset.UtcNow with get,set

    let private genPartitionKey (postId : string) = 
        let postId =
            postId.Replace('/', '.').Replace('\\','.').Replace('#','.').Replace('?', '.')
        sprintf "post-%s" postId

    type CommentStorage(settings) =
        let table = 
                CloudStorageAccount.Parse(settings.StorageConnectionString)
                    .CreateCloudTableClient()
                    .GetTableReference(settings.TableName)

        let commentFromRow (row: CommentRow) =
            { time = row.CreatedAt
              name = row.Name
              comment = row.CommentHtml }

        member __.GetCommentsForPost(postId) =
            let postKey = genPartitionKey postId
            let q =
                TableQuery<CommentRow>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, postKey))
            table.ExecuteQuery(q)
            |> Seq.map commentFromRow
            |> Array.ofSeq
            |> Array.sortBy (fun c -> c.time)

        member __.AddCommentForPost(comment: PendingComment) =
            let commentRow = CommentRow(PartitionKey = (genPartitionKey comment.postid),
                                        RowKey = Guid.NewGuid().ToString(),
                                        Name = comment.name,
                                        CommentHtml = comment.commentHtml,
                                        CommentRaw = comment.commentRaw,
                                        IpAddress = comment.ipAddress,
                                        CreatedAt = comment.createdAt)

            let result = table.Execute(TableOperation.Insert(commentRow))
            (result.Result :?> CommentRow) |> commentFromRow
