function Comments(postId) {
    var url = "[AZURE FUNCTION URL]?postid=" + postId

    $(document).ready(function () {
        $.ajax(url, {
            headers: {},
            dataType: "json",
            success: function (comments) {
                $.each(comments, function (i, comment) {
                    var date = new Date(comment.time);
                    var t = "<div class='commentsaf-comment'>";
                    t += "<div class='commentsaf-commentheader'><b>" + comment.name + "</b>";
                    t += " posted at ";
                    t += "<em>" + date.toLocaleString() + "</em></div>";
                    t += "<div class='commentsaf-commentbody'>";
                    t += comment.comment;
                    t += "</div>";
                    $("#comments").append(t);
                });
            },
            error: function () {
                $("#comments").append("Error retrieving comments.");
            }
        });
    });
}