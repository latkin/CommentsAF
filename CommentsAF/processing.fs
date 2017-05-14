namespace CommentsAF

open System
open Ganss.XSS
open Markdig
open Microsoft.Azure.WebJobs.Host

exception ProcessingExn of string

module Processing =
    let private bodySanitizer = HtmlSanitizer()
    let private nameSanitizer = HtmlSanitizer(allowedTags = [])
    let private mdPipeline =
        MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .Build()

    let private markdownToHtml str = Markdown.ToHtml(str, mdPipeline)

    let private sanitizeBody str = bodySanitizer.Sanitize(str)

    let private sanitizeName str = nameSanitizer.Sanitize(str)

    let private check name maxLen str =
        let err = sprintf "Invalid value for %s" name
        if String.IsNullOrWhiteSpace(str) || str.Length > maxLen then
            raise (ProcessingExn(err))
        else str

    let userCommentToPending (log: TraceWriter) (userComment : UserComment) : PendingComment =
        let finalName =
            userComment.name
            |> sanitizeName
            |> check "name" 100

        log.Info(sprintf "Original comment name: %s" userComment.name)
        log.Info(sprintf "Final comment body: %s" finalName)

        let htmlComment =
            userComment.comment
            |> markdownToHtml
            |> sanitizeBody
            |> check "comment" 2048

        log.Info(sprintf "Original comment body: %s" userComment.comment)
        log.Info(sprintf "Final comment body: %s" htmlComment)

        { PendingComment.postid = userComment.postid
          name = finalName
          comment = htmlComment }