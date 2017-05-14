var azureFunctionUrl = "[AZURE FUNCTION URL]";
var commentsaf_postid = "nil";

function CommentsAF(postId) {
    commentsaf_postid = postId;
    $("#caf-comments").append("<div id='caf-commentlist'/>");
    $("#caf-comments").append("<div id='caf-submitcomment'/>");

    LoadComments();
    LoadCommentSubmission();
}

function LoadCommentSubmission() {
    var t = "<form>";
    t += "<fieldset>";
    t += "<legend>Leave a comment</legend>";
    t += "Name<br>";
    t += "<input type='text' id='caf-submit-name'><br>";
    t += "Comment (markdown supported)<br>";
    t += "<textarea id='caf-submit-comment' cols='80' rows='10' /><br><br>";
    t += "<input type='submit' id='caf-submit-postcomment' value='Post comment'>";
    t += "</fieldset>";
    t += "</form>";

    $("#caf-submitcomment").append(t);

    $("#caf-submit-postcomment").click(function (e) {
        e.preventDefault();
        $("#caf-submit-name").prop("disabled", true);
        $("#caf-submit-comment").prop("disabled", true);
        $("#caf-submit-postcomment").prop("disabled", true);

        AddComment(commentsaf_postid, $("#caf-submit-name").val(), $("#caf-submit-comment").val());
    });
}

function AddSingleComment(comment) {
    var date = new Date(comment.time);
    var t = "<div class='caf-comment'>";
    t += "<div class='caf-commentheader'><b>" + comment.name + "</b>";
    t += " posted at ";
    t += "<em>" + date.toLocaleString() + "</em></div>";
    t += "<div class='caf-commentbody'>";
    t += comment.comment;
    t += "</div>";
    $("#caf-commentlist").append(t);
}

function LoadComments() {
    var url = azureFunctionUrl + "?postid=" + commentsaf_postid;
    $.ajax(url, {
        headers: {},
        dataType: "json",
        success: function (comments) {
            $.each(comments, function (i, comment) {
                AddSingleComment(comment);
            });
        },
        error: function () {
            $("#caf-commentlist").append("Error retrieving comments.");
        }
    });
}

function AddComment(postId, name, comment) {
    var data = JSON.stringify({ postid: postId, name: name, comment: comment })
    $.ajax(azureFunctionUrl, {
        data: data,
        type: "POST",
        contentType: "application/json",
        success: function (data, status, xhr) {
            AddSingleComment(data);
            $("#caf-submit-name").val("");
            $("#caf-submit-comment").val("");
            $("#caf-submit-name").prop("disabled", false);
            $("#caf-submit-comment").prop("disabled", false);
            $("#caf-submit-postcomment").prop("disabled", false);
        },
        error: function (err) {
            var data = $.parseJSON(err.responseText);
            if (data.Message) {
                $("#caf-commentlist").append("<div class='caf-submit-error'>Error adding comment: " + data.Message + "</div>");
            } else {
                $("#caf-commentlist").append("<div class='caf-submit-error'>Error adding comment</div>");
            }

            $("#caf-submit-name").prop("disabled", false);
            $("#caf-submit-comment").prop("disabled", false);
            $("#caf-submit-postcomment").prop("disabled", false);
        },
        dataType: "json"
    });
}