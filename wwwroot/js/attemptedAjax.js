$(document).ready(function () {

    $('.btn-success').on('click', function () {
        console.log("btn click detected in javascript.");
        const data = 123;
        $.post('/Ajax/AjaxPostOne', function (result)
        {
            console.log(result);
            $('#randomBox').text(result);
        });
    })

});