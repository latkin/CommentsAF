namespace CommentsAF
open Markdig

module Processing =
    
    let private mdPipeline =
        MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .Build()

    let userCommentToPending (userComment : UserComment) =
        let htmlComment = Markdown.ToHtml(userComment.comment, mdPipeline)
        { PendingComment.postid = userComment.postid
          name = userComment.name
          comment = htmlComment }